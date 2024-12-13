using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contrib.Xunit.ActivityListenerTestFramework.Tests;

public class SystemUnderTest
{
    public static ActivitySource TracingSource = new ActivitySource(nameof(SystemUnderTest));

    public int TraceSome(int echo)
    {
        var toReturn = echo;
        using var activity = TracingSource.StartActivity(nameof(TraceSome), ActivityKind.Internal);

        activity?.AddTag("TagKey1", "Value1");
        activity?.AddTag("RunCount", toReturn++);

        return toReturn;
    }
}
