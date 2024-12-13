namespace Contrib.Xunit.ActivityListenerTestFramework;

using global::Xunit;
using global::Xunit.Abstractions;
using global::Xunit.Sdk;
using System.Diagnostics;
using System.Reflection;
using System.Security;

public class ActivityCoverageTestAssemblyRunner : XunitTestAssemblyRunner
{
    private bool disableParallelization;
    private int maxParallelThreads;
    private bool initialized;
    private SemaphoreSlim? parallelSemaphore;
    private SynchronizationContext? originalSyncContext;
    private ParallelAlgorithm parallelAlgorithm;

    public ActivityCoverageTestAssemblyRunner(
        ITestAssembly testAssembly,
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink diagnosticMessageSink,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
        : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
    {
    }

    protected override string GetTestFrameworkEnvironment()
    {
        this.InitializeRunner();
        return base.GetTestFrameworkEnvironment();
    }

    protected virtual void InitializeRunner()
    {
        if (this.initialized)
        {
            return;
        }

        var collectionBehaviorAttribute = TestAssembly.Assembly.GetCustomAttributes(typeof(CollectionBehaviorAttribute)).SingleOrDefault();
        if (collectionBehaviorAttribute != null)
        {
            this.disableParallelization = collectionBehaviorAttribute.GetNamedArgument<bool>("DisableTestParallelization");
            this.maxParallelThreads = collectionBehaviorAttribute.GetNamedArgument<int>("MaxParallelThreads");
        }

        disableParallelization = this.ExecutionOptions.DisableParallelization() ?? disableParallelization;
        parallelAlgorithm = this.ExecutionOptions.ParallelAlgorithmOrDefault();
        this.initialized = true;
    }



    /// <inheritdoc/>
    protected override Task BeforeTestAssemblyFinishedAsync()
    {
        if (this.originalSyncContext != null)
        {
            SetSynchronizationContext(this.originalSyncContext);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async Task<RunSummary> RunTestCollectionsAsync(IMessageBus messageBus, CancellationTokenSource cancellationTokenSource)
    {
        this.originalSyncContext = SynchronizationContext.Current;

        if (disableParallelization)
            return await this.RunTestCollectionsSerialAsync(messageBus, cancellationTokenSource);

        if (this.parallelAlgorithm == ParallelAlgorithm.Aggressive)
            SetupSyncContext(maxParallelThreads);
        else
            SetupParallelSemaphore(maxParallelThreads);

        Func<Func<Task<RunSummary>>, Task<RunSummary>> taskRunner;
        if (SynchronizationContext.Current != null)
        {
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            taskRunner = code => Task.Factory.StartNew(code, cancellationTokenSource.Token, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler, scheduler).Unwrap();
        }
        else
            taskRunner = code => Task.Run(code, cancellationTokenSource.Token);

        var summaries = new List<RunSummary>();

        var testCollections = OrderTestCollections();

        var segregatedTests = testCollections.SelectMany(e => e.Item2, (ordered, testCase) => (collection: ordered.Item1, testCase: testCase))
            .GroupBy(e => e.testCase is IActivityCoverageTestCase, (k, items) => (isActivityCoverage: k, items: items));

        List<(ITestCollection, IEnumerable<IXunitTestCase>)> regularTests = new();
        IEnumerable<(string activitySource, IEnumerable<(ITestCollection, IEnumerable<IXunitTestCase>)> testCollections)>? activityTests = default;

        foreach (var item in segregatedTests)
        {
            if (item.isActivityCoverage)
            {
                activityTests = item.items
                    .Select(e => (collection: e.collection, testCase: e.testCase as IActivityCoverageTestCase))
                    .GroupBy(e => e.testCase!.ActivitySource, (k, i) => (activitySource: k, testCollections: i.GroupBy(c => c.collection, (ck, tests) => (ck, tests.Select(t => (t.testCase as IXunitTestCase)!)))));
            }
            else
            {
                regularTests.AddRange(item.items.GroupBy(e => e.collection, (k, tests) => (k, tests.Select(t => t.testCase))));
            }
        }

        await RunTestCollectionGroup(messageBus, cancellationTokenSource, taskRunner, summaries, regularTests);

        if (activityTests != null)
        {
            foreach (var item in activityTests)
            {
                var source = item.activitySource;
                using var activityListener = new ActivityListener()
                {
                    ShouldListenTo = (a) => a.Name == source,
                    Sample = (ref ActivityCreationOptions<ActivityContext> s) => { return ActivitySamplingResult.AllDataAndRecorded; }
                };

                ActivitySource.AddActivityListener(activityListener);
                await RunTestCollectionGroup(messageBus, cancellationTokenSource, taskRunner, summaries, item.testCollections);
            }
        }

        return new RunSummary()
        {
            Total = summaries.Sum(s => s.Total),
            Failed = summaries.Sum(s => s.Failed),
            Skipped = summaries.Sum(s => s.Skipped)
        };

        async Task RunTestCollectionGroup(IMessageBus messageBus, CancellationTokenSource cancellationTokenSource, Func<Func<Task<RunSummary>>, Task<RunSummary>> taskRunner, List<RunSummary> summaries, IEnumerable<(ITestCollection, IEnumerable<IXunitTestCase>)> testCollections)
        {
            List<Task<RunSummary>>? parallel = null;
            List<Func<Task<RunSummary>>>? nonParallel = null;
            foreach (var collection in testCollections)
            {
                Func<Task<RunSummary>> task = () => RunTestCollectionAsync(messageBus, collection.Item1, collection.Item2, cancellationTokenSource);

                // attr is null here from our new unit test, but I'm not sure if that's expected or there's a cheaper approach here
                // Current approach is trying to avoid any changes to the abstractions at all
                var attr = collection.Item1.CollectionDefinition?.GetCustomAttributes(typeof(CollectionDefinitionAttribute)).SingleOrDefault();
                if (attr?.GetNamedArgument<bool>(nameof(CollectionDefinitionAttribute.DisableParallelization)) == true)
                {
                    (nonParallel ??= new List<Func<Task<RunSummary>>>()).Add(task);
                }
                else
                {
                    (parallel ??= new List<Task<RunSummary>>()).Add(taskRunner(task));
                }
            }

            if (parallel?.Count > 0)
            {
                foreach (var task in parallel)
                {
                    try
                    {
                        summaries.Add(await task);
                    }
                    catch (TaskCanceledException) { }
                }
            }

            if (nonParallel?.Count > 0)
            {
                foreach (var task in nonParallel)
                {
                    try
                    {
                        summaries.Add(await taskRunner(task));
                        if (cancellationTokenSource.IsCancellationRequested)
                            break;
                    }
                    catch (TaskCanceledException) { }
                }
            }
        }
    }

    private void SetupParallelSemaphore(int maxParallelThreads)
    {
        if (maxParallelThreads < 1)
            return;

        this.parallelSemaphore = new SemaphoreSlim(maxParallelThreads);

#if NETSTANDARD
            var type = Type.GetType("System.Threading.ThreadPool");
            if (type is null)
                throw new InvalidOperationException("Cannot find type: System.Threading.ThreadPool");

            var getMethod = type.GetRuntimeMethod("GetMinThreads", new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() });
            if (getMethod is null)
                throw new InvalidOperationException("Cannot find method: System.Threading.ThreadPool.GetMinThreads");

            var args = new object[] { 0, 0 };
            getMethod.Invoke(null, args);

            var minThreads = (int)args[0];
            var minIOPorts = (int)args[1];

            var threadFloor = Math.Min(4, maxParallelThreads);
            if (minThreads < threadFloor)
            {
                var setMethod = type.GetRuntimeMethod("SetMinThreads", new[] { typeof(int), typeof(int) });
                if (setMethod is null)
                    throw new InvalidOperationException("Cannot find method: System.Threading.ThreadPool.SetMinThreads");

                setMethod.Invoke(null, new object[] { threadFloor, minIOPorts });
            }
#else
        ThreadPool.GetMinThreads(out var minThreads, out var minIOPorts);
        var threadFloor = Math.Min(4, maxParallelThreads);
        if (minThreads < threadFloor)
            ThreadPool.SetMinThreads(threadFloor, minIOPorts);
#endif
    }

    protected override async Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
    {
        if (parallelSemaphore is not null)
            await parallelSemaphore.WaitAsync(cancellationTokenSource.Token);

        try
        {
            return await new XunitTestCollectionRunner(testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource).RunAsync();
        }
        finally
        {
            parallelSemaphore?.Release();
        }
    }

    protected virtual async Task<RunSummary> RunTestCollectionsSerialAsync(IMessageBus messageBus, CancellationTokenSource cancellationTokenSource)
    {
        var summary = new RunSummary();

        var testCollections = OrderTestCollections();

        var segregatedTests = testCollections.SelectMany(e => e.Item2, (ordered, testCase) => (collection: ordered.Item1, testCase: testCase))
            .GroupBy(e => e.testCase is IActivityCoverageTestCase, (k, items) => (isActivityCoverage: k, items: items));

        List<(ITestCollection, IEnumerable<IXunitTestCase>)> regularTests = new();
        IEnumerable<(string activitySource, IEnumerable<(ITestCollection, IEnumerable<IXunitTestCase>)> testCollections)>? activityTests = default;
        foreach (var item in segregatedTests)
        {
            if (item.isActivityCoverage)
            {
                activityTests = item.items
                    .Select(e => (collection: e.collection, testCase: e.testCase as IActivityCoverageTestCase))
                    .GroupBy(e => e.testCase!.ActivitySource, (k, i) => (activitySource: k, testCollections: i.GroupBy(c => c.collection, (ck, tests) => (ck, tests.Select(t => (t.testCase as IXunitTestCase)!)))));
            }
            else
            {
                regularTests.AddRange(item.items.GroupBy(e => e.collection, (k, tests) => (k, tests.Select(t => t.testCase))));
            }
        }


        foreach (var collection in regularTests)
        {
            summary.Aggregate(await RunTestCollectionAsync(messageBus, collection.Item1, collection.Item2, cancellationTokenSource));
            if (cancellationTokenSource.IsCancellationRequested)
                break;
        }

        if (activityTests != null)
        {
            foreach (var item in activityTests)
            {
                var source = item.activitySource;
                using var activityListener = new ActivityListener()
                {
                    ShouldListenTo = (a) => a.Name == source,
                    Sample = (ref ActivityCreationOptions<ActivityContext> s) => { return ActivitySamplingResult.AllDataAndRecorded; }
                };

                ActivitySource.AddActivityListener(activityListener);

                foreach (var collection in item.testCollections)
                {
                    summary.Aggregate(await RunTestCollectionAsync(messageBus, collection.Item1, collection.Item2, cancellationTokenSource));
                    if (cancellationTokenSource.IsCancellationRequested)
                        break;
                }
            }
        }

        return summary;
    }

    [SecuritySafeCritical]
    static void SetSynchronizationContext(SynchronizationContext context)
        => SynchronizationContext.SetSynchronizationContext(context);
}
