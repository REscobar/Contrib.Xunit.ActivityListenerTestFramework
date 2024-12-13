# Contrib.Xunit.ActivityListenerTestFramework
This allows you to test the branching paths of tracing using `System.Diagnostics.Activity`

## Motivation
When creating observable libraries or applications you will very likely be using intrumenting your code using `System.Diagnostics.Activity` and `System.Diagnostics.ActivitySource`, this creates a situation that when there are no listeners your activity source will create null activities and the code will have many null check branches like the following

```csharp
//Sample taken from: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-otlp-example#3-create-an-api-endpoint
async Task<String> SendGreeting(ILogger<Program> logger)
{
    // Create a new Activity scoped to the method
    using var activity = greeterActivitySource.StartActivity("GreeterActivity");

    // Add a tag to the Activity
    activity?.SetTag("greeting", "Hello World!"); //Null conditional method call

    return "Hello World!";
}
```

The nullability check is a branch in the code execution and will be marked as a partially covered line in most code coverage reports unless the method is tested once while listening to the activity and once while not.

Unfortunately the activiy source and activity listeners are considered static scope thus creating a challenge when you want to achieve higher code coverage and these branching paths could actually be a source of bugs if not tested correctly, consider the following (oversimplified) snippet

```csharp
//Sample taken from: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-otlp-example#3-create-an-api-endpoint
async Task<String> SendGreeting(ILogger<Program> logger)
{
    // Create a new Activity scoped to the method
    using var activity = greeterActivitySource.StartActivity("GreeterActivity");

    var someObject = null;

    // Add a tag to the Activity
    activity.SetTag("greeting", "Hello World!"); //This will throw without listeners (No nullability check on activity)
    
    activity?.SetTag("someProp", someObject.Prop1); //This will throw whith listeners (No nullability check on someObject)

    return "Hello World!";
}
```
## Solution
Run the test while listening to the activity sources and again while not


## How to use
Install package 
```
dotnet add package Contrib.Xunit.ActivityListenerTestFramework
```

Replace `[Fact]` attributes with `[ActivityCoverageFact]` and `[Theory]` attributes with `[ActivityCoverageTheory]` of the test that you want to get activity coverage, each accept a list of activity sources that will be sequencially listened on to enable coverage on the branching paths that are a result of activity nullability when there are no listeners, use an empty string (`""`) to test with no listeners attached.

```csharp
[ActivityCoverageFact("", nameof(SystemUnderTest), "RandomActitySource")] //Once with no listener, once listening to SystemUnderTest, once listening to RandomActivitySource
public void FactTest()
{
    var subject = new SystemUnderTest();

    var result = subject.TraceSome();
}

[ActivityCoverageFact] // Testing with an empty list of activity sources is also supported, this will prodcue equivalent behavior compared to the standard [Fact]
public void FactTest()
{
    var subject = new SystemUnderTest();

    var result = subject.TraceSome();
}

[Fact] //[Theory] These still works as expected and are not affected at all by the custom runner
public void RegularFactTest()
{
    var subject = new SystemUnderTest();

    var result = subject.TraceSome();
}
```

This will create a test case for each activity source, you may use an unbounded amount of sources, the test cases will be run while listening to each activity source in turn and you will achieve a higher amount of line and branch coverage.

> [!Tip]
> Write your test as usual, don't focus on asserting the traceabilty


## Limitations
This replaces the built-in assembly runner from xUnit, this means that you can not use this in conjuction with other packages/solutions that require a custom runner

## Acknowlledgement

Special thanks to authors and mantainers of xUnit for their support.

Ideas taken from:
 - https://github.com/xunit/xunit/discussions/3076
 - https://github.com/meziantou/Meziantou.Xunit.ParallelTestFramework
 - https://github.com/xunit/xunit/issues/1986#issuecomment-831322722