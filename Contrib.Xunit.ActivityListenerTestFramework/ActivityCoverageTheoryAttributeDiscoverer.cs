namespace Contrib.Xunit.ActivityListenerTestFramework;

using global::Xunit.Abstractions;
using global::Xunit.Sdk;

public class ActivityCoverageTheoryAttributeDiscoverer : TheoryDiscoverer
{
    private readonly IMessageSink diagnosticMessageSink;

    public ActivityCoverageTheoryAttributeDiscoverer(IMessageSink diagnosticMessageSink)
        : base(diagnosticMessageSink)
    {
        this.diagnosticMessageSink = diagnosticMessageSink;
    }

    protected override IEnumerable<IXunitTestCase> CreateTestCasesForDataRow(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo theoryAttribute, object[] dataRow)
    {
        var sources = GetActivitySource(theoryAttribute);
        return sources.Select(source => new ActivityCoverageTheoryTestCase(DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod, source, dataRow)).ToList();
    }

    protected override IEnumerable<IXunitTestCase> CreateTestCasesForTheory(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo theoryAttribute)
    {
        var sources = GetActivitySource(theoryAttribute);
        return sources.Select(source => new ActivityCoverageTheoryTestCase(DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod, source)).ToList();
    }

    static string[] GetActivitySource(IAttributeInfo activityCoverageTheoryAttribute)
    {
        var ctorArgs = activityCoverageTheoryAttribute.GetConstructorArguments().ToArray();
        var sources = Reflector.ConvertArguments(ctorArgs, new[] { typeof(string[]) }).Cast<string[]>().Single();

        if (sources == null || sources.Length == 0)
            sources = [string.Empty];

        return sources;
    }
}

