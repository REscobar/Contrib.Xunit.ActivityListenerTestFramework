namespace Contrib.Xunit.ActivityListenerTestFramework;

using global::Xunit.Abstractions;
using global::Xunit.Sdk;

public class ActivityCoverageFactAttributeDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink diagnosticMessageSink;

    public ActivityCoverageFactAttributeDiscoverer(IMessageSink diagnosticMessageSink)
    {
        this.diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
    {
        var ctorArgs = factAttribute.GetConstructorArguments().ToArray();
        var sources = Reflector.ConvertArguments(ctorArgs, new[] { typeof(string[]) }).Cast<string[]>().Single();

        if (sources is null || sources.Length == 0)
            sources = [string.Empty];

        var methodDisplay = discoveryOptions.MethodDisplayOrDefault();
        var methodDisplayOptions = discoveryOptions.MethodDisplayOptionsOrDefault();

        return sources.Select(source => new ActivityCoverageTestCase(diagnosticMessageSink, methodDisplay, methodDisplayOptions, testMethod, source)).ToList();
    }
}
