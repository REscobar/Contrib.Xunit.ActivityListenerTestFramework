namespace Contrib.Xunit.ActivityListenerTestFramework;

using global::Xunit.Abstractions;
using global::Xunit.Sdk;
using System.Reflection;

public class ActivityCoverageTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    public ActivityCoverageTestFrameworkExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
    {
        using var assemblyRunner = new ActivityCoverageTestAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
        await assemblyRunner.RunAsync();
    }
}

