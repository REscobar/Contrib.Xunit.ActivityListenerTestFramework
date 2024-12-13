namespace Contrib.Xunit.ActivityListenerTestFramework;

using global::Xunit.Abstractions;
using global::Xunit.Sdk;
using System.Reflection;

public class ActivityCoverageTestFramework : XunitTestFramework
{
    public ActivityCoverageTestFramework(IMessageSink messageSink) : base(messageSink)
    {
    }

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
    {
        return new ActivityCoverageTestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
    }
}

