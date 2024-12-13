namespace Contrib.Xunit.ActivityListenerTestFramework;

using global::Xunit;
using global::Xunit.Sdk;

[XunitTestCaseDiscoverer("Contrib.Xunit.ActivityListenerTestFramework.ActivityCoverageFactAttributeDiscoverer", "Contrib.Xunit.ActivityListenerTestFramework")]
public class ActivityCoverageFactAttribute : FactAttribute
{
    public ActivityCoverageFactAttribute(params string[] activitySources)
    {
        ActivitySources = activitySources;
    }

    public string[] ActivitySources { get; }
}

