# JSON Source Generation Support

> [!NOTE]
> The following information is only relevant if you are using your own record types with an `AtProtoAgent`, calling the `AtProtoServer` methods directly or
> using the `AtProtoHttpClient` directly.
> If you are using a `BlueskyAgent` you do not need to take any action.

If you are using your own record types with an `AtProtoAgent` Native AOT requires 
[JSON serialization source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation).

### Configurating the agent for source generation

You must configure the `AtProtoAgent` to use the source generation context either for your classes via `JsonOptions`,

```c#
JsonOptions httpJsonOptions = new();
httpJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);

using (AtProtoAgent agent = new(
    service: server,
    options: new ()
    {
        HttpJsonOptions = httpJsonOptions
    }))
{
    // Your code to use the agent
}
```

or through the agent builder

```c#
var service = new Uri("https://pds.example.com");

var builder = AtProtoAgent.CreateBuilder();
    .ForService(service)
    .ConfigureJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, YourAppJsonSerializerContext.Default);
    });

using var agent = agent.Build()
{
    // Your code to use the agent
};

```

### Calling `AtProtoServer` and `AtProtoHttpClient` methods directly.

If you are using the generic static server methods which a `AtProtoRecord` or `AtProtoRecordType` you must use the method overloads which take a `jsonSerializerOptions` parameter.
The `jsonSerializationOptions` value must be a a chained instance which adds the type resolver for your classes to the type resolved for the classes `AtProtoServer` uses internally.
To create a chained instance of `JsonSerializationOptions` call `AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions()`
and passing in the `JsonSerializerOptions.Default` from your code where you have JSON source generation configured

### Annotating your own classes for json source generation

The reference `AtProto` implementation is quite laissez faire about where the `$type` property appears in serialized JSON. Make sure that the
`AllowOutOfOrderMetadataProperties` is set to `true`` in your `JsonSourceGenerationOptions` on your `SourceGenerationContext` class. For example,

```c#
    [JsonSourceGenerationOptions(
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false,
        UseStringEnumConverter = true)]
```

You must also ensure both your record classes (those deriving from `AtProtoRecord`) and your record value classes (those deriving from `AtProtoRecordValue`) are marked
as `JsonSerializable` in your `JsonSerializerContext`. Additionally you must also add the `AtProtoRecord<YourRecordValue>` class to the context.

For example, if you have a custom `AtProtoRecordValue` and corresponding `AtProtoRecord<TRecordValue>` it should look something like the following.

```c#
    public record CustomRecordValue : AtProtoRecordValue
    {
        public required string CustomRecordProperty { get; set; }
    }

    public record CustomAtProtoRecord : AtProtoRecord<CustomRecordValue>
    {
        public CustomAtProtoRecord(AtUri uri, Cid cid, CustomRecordValue value) : base(uri, cid, value)
        {
        }
    }

    [JsonSourceGenerationOptions(
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false,
        UseStringEnumConverter = true)]

    [JsonSerializable(typeof(CustomAtProtoRecord))]
    [JsonSerializable(typeof(CustomRecordValue))]
    [JsonSerializable(typeof(AtProtoRecord<CustomRecordValue>))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
```

# Native AOT

`idunno.AtProto` and `idunno.Bluesky` support [.NET native ahead-of-time (AOT)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).

Native AOT has benefits:

* Reduced disk footprint: Native AOT publishing produces a single executable containing just the code from external dependencies that is used by your application.
* Reduced startup time: Native AOT can show reduced start-up times, meaning your app is ready to respond more quickly.
* Reduced memory usage: Native AOT apps can show reduced memory demands, depending on the work it does.

If you wish to use AOT with your own classes you must enable JSON Source Generation for your `AtProtoRecordValue` and `AtProtoRecord` records and follow the guidance above.

# Trimming

Trimming is supported if your application targets .NET 9.0. You cannot target .NET 8.0 due the use of v9 of `System.Text.Json` and
a [bug in the linker](https://github.com/dotnet/runtime/issues/114307).
