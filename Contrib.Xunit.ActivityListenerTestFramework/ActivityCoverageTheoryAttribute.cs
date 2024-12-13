namespace Contrib.Xunit.ActivityListenerTestFramework;

using global::Xunit;
using global::Xunit.Sdk;

[XunitTestCaseDiscoverer("Contrib.Xunit.ActivityListenerTestFramework.ActivityCoverageTheoryAttributeDiscoverer", "Contrib.Xunit.ActivityListenerTestFramework")]
public class ActivityCoverageTheoryAttribute : TheoryAttribute
{
    public ActivityCoverageTheoryAttribute(params string[] activitySources)
    {
        this.ActivitySources = activitySources;
    }

    public string[] ActivitySources { get; }
}

