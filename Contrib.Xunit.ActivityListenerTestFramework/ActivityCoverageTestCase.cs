namespace Contrib.Xunit.ActivityListenerTestFramework;

using global::Xunit.Abstractions;
using global::Xunit.Sdk;
using System.ComponentModel;

public class ActivityCoverageTestCase : XunitTestCase, IActivityCoverageTestCase
{
    private string? source;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public ActivityCoverageTestCase()
        : base()
    {

    }

    public ActivityCoverageTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay methodDisplay, TestMethodDisplayOptions methodDisplayOptions, ITestMethod testMethod, string source)
        : base(diagnosticMessageSink, methodDisplay, methodDisplayOptions, testMethod)
    {
        this.source = source;
        this.Initialize(source);
    }

    public string ActivitySource
    {
        get
        {
            return this.source!;
        }
    }

    private void Initialize(string source)
    {
        this.source = source;

        if (source.Length > 0)
        {
            DisplayName += $" [ActivitySource: {source}]";
            Add(Traits, "ActivitySource", source);
        }
        else
        {
            DisplayName += " - No Activity Listener";
            Add(Traits, "ActivitySource", "- No Listener -");
        }
    }

    protected override string GetUniqueID()
       => $"{base.GetUniqueID()}-{this.source}";


    public override void Deserialize(IXunitSerializationInfo data)
    {
        base.Deserialize(data);

        Initialize(data.GetValue<string>("ActivitySource"));
    }

    public override void Serialize(IXunitSerializationInfo data)
    {
        base.Serialize(data);

        data.AddValue("ActivitySource", this.source);
    }


    public static void Add<TKey, TValue>(Dictionary<TKey, List<TValue>> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        AddOrGet(dictionary, key).Add(value);
    }

    public static TValue AddOrGet<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
        where TValue : new()
    {
        return AddOrGet(dictionary, key, () => new TValue());
    }

    public static TValue AddOrGet<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key, Func<TValue> newValue)
        where TKey : notnull
    {

        if (!dictionary.TryGetValue(key, out TValue? result))
        {
            result = newValue();
            dictionary[key] = result;
        }

        return result;
    }
}
