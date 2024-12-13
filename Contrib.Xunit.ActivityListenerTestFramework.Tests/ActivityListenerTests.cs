using global::Xunit;
namespace Contrib.Xunit.ActivityListenerTestFramework.Tests;

public class ActivityListenerTests
{
    private static int FactTestRunCount = 0;
    private static int NoListenerRunCount = 0;
    private static int RegularTestRunCount = 0;

    [ActivityCoverageFact("", nameof(SystemUnderTest))] //Once with no listener, once listening to SystemUnderTest
    public void FactTest()
    {
        var subject = new SystemUnderTest();

        var result = subject.TraceSome(FactTestRunCount);
        var expected = FactTestRunCount++ == 0 ? 0 : FactTestRunCount;

        Assert.Equal(expected, result);
    }

    [ActivityCoverageFact(nameof(SystemUnderTest))]
    public void HasListener()
    {
        var expected = true;

        Assert.Equal(expected, SystemUnderTest.TracingSource.HasListeners());
    }

    [ActivityCoverageFact("RandomActivitySource")]
    public void DoesNotHaveListeners()
    {
        var expected = false;

        Assert.Equal(expected, SystemUnderTest.TracingSource.HasListeners());
    }

    [ActivityCoverageFact]
    public void NoListener()
    {
        var subject = new SystemUnderTest();

        var result = subject.TraceSome(NoListenerRunCount);
        NoListenerRunCount++;
        var expected = 0;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void RegularTest()
    {
        var subject = new SystemUnderTest();

        var result = subject.TraceSome(RegularTestRunCount);
        RegularTestRunCount++;
        var expected = 0;

        Assert.Equal(expected, result);
    }

    [ActivityCoverageTheory("", nameof(SystemUnderTest))]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheoryTest(int index)
    {
        var subject = new SystemUnderTest();
        var result = subject.TraceSome(index);

        var expected = index;
        if (SystemUnderTest.TracingSource.HasListeners())
        {
            expected++;
        }

        Assert.Equal(expected, result);
    }

    [ActivityCoverageTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheoryTestNoListener(int index)
    {
        var subject = new SystemUnderTest();
        var result = subject.TraceSome(index);

        var expected = index - 1;
        if (!SystemUnderTest.TracingSource.HasListeners())
        {
            expected++;
        }

        Assert.Equal(expected, result);
    }
}