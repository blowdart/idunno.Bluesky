# JSON Source Generation Support

> [!NOTE]
> The following information is only relevant if you are using your own record types with an `AtProtoAgent`, calling the `AtProtoServer` methods directly or
> using the `AtProtoHttpClient` directly.
> If you are using a `BlueskyAgent` you do not need to take any action.

If you are using your own record types with an `AtProtoAgent` Native AOT requires 
[JSON serialization source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation).

### Unknown JSON properties

The SDK's JSON options tolerate unknown properties whose names begin with `$`, such as `"$via": "kazahana"`.
This applies to direct deserialization with `BlueskyJsonSerializerOptions.Default` or `.Options`,
`AtProtoJsonSerializerOptions`, the server options, chained SDK options and agent HTTP JSON options, including nested
posts, embeds, feed views and typed repository envelopes.

Unknown properties are retained, with their original names and JSON values, when the selected concrete contract has
`[JsonExtensionData]`. Otherwise they are discarded. `$type` still selects the contract; unrecognized discriminators use
the contract's existing fallback policy. Malformed discriminators and invalid or missing required fields still fail.
The protocol's `$link` and `$bytes` representations are unchanged.

The tolerant decoder uses source-generated type metadata, without reflection-based type construction, and supports
trimmed and Native AOT applications on .NET 9 and .NET 10. Custom JSON
converters retain control of their own wire formats and receive the SDK options for decoding nested values, including
when a custom converter is registered for a value type or a property. Type-attached reference converters retain their
null-handling policies, including null sentinels and null validation. Agent constructors copy and finalize JSON options
after caller configuration, including builder callbacks and later changes to builder options made before `Build()`.
The agent's serializer options are read-only; the original caller options remain unchanged and mutable.
Using a source-generation context directly, rather than the SDK options, does not enable tolerant decoding.

### Configuring options for direct serialization

Use `AtProtoJsonSerializerOptions.Create(configure)` or `BlueskyJsonSerializerOptions.Create(configure)` to configure
mutable options before the SDK installs tolerant converters and wraps the final resolver chain:

```c#
JsonSerializerOptions options = BlueskyJsonSerializerOptions.Create(options =>
{
    options.TypeInfoResolverChain.Insert(0, YourAppJsonSerializerContext.Default);
    options.WriteIndented = true;
});
```

The result is a new read-only snapshot. Configuration callbacks may prepend or replace resolvers; if replacing the
entire resolver, include every context needed by your application. Custom settings, converters and metadata modifiers
are preserved. Shared `Default` options remain read-only and unchanged. Do not serialize using the callback's options;
SDK adaptation is installed only on the returned copy after the callback completes.
Resolver and converter instances are shared rather than deep-cloned; do not mutate those objects after finalization.

> [!WARNING]
> The legacy `.Options` properties, `new JsonOptions()`, and `BuildChainedTypeInfoResolverJsonSerializerOptions(...)`
> return mutable options for compatibility. Arbitrary mutations to already-returned `JsonSerializerOptions` are not
> intercepted. Appending a resolver is supported, but prepending a resolver, replacing a resolver that precedes the SDK
> wrapper, replacing `TypeInfoResolver`, or customizing a copy made with `new JsonSerializerOptions(...)` can bypass
> SDK contract adaptation. Deserialization may throw rather than tolerate unknown metadata. Prefer the `Create`
> callbacks or pass configured `JsonOptions` to an agent constructor, which finalizes its own copy.

For legacy direct-call code, use `AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(customizedOptions)`
**after** resolver customization to restore adaptation around the customized resolver. This helper returns fresh mutable
AtProto defaults and chains the supplied resolver; it does not copy the supplied settings or converters. Reapply those
settings to its result if needed, or use a `Create` callback instead. Later arbitrary resolver mutations can bypass the
wrapper again. No API promises to intercept arbitrary mutations of mutable returned options.

### Configuring the agent for source generation

You must configure the `AtProtoAgent` to use the source generation context either for your classes via `JsonOptions`,

```c#
JsonOptions httpJsonOptions = new();
httpJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Add(SourceGenerationContext.Default);

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

var builder = AtProtoAgent.CreateBuilder()
    .ForService(service)
    .ConfigureHttpJsonOptions(options =>
    {
        options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, YourAppJsonSerializerContext.Default);
    });

using var agent = builder.Build();

// Your code to use the agent
```

### Calling `AtProtoServer` and `AtProtoHttpClient` methods directly.

If you are using the generic static server methods which take an `AtProtoRecord` or `AtProtoRecordType` you must use the method overloads which take a `jsonSerializerOptions` parameter.
The `jsonSerializerOptions` value must be a chained instance which adds the type resolver for your classes to the type resolver for the classes `AtProtoServer` uses internally.
To create a chained instance of `JsonSerializerOptions` call `AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions()`,
passing in the `SourceGenerationContext.Default` from your code where you have JSON source generation configured.

### Annotating your own classes for json source generation

The reference `AtProto` implementation is quite laissez faire about where the `$type` property appears in serialized JSON. Make sure that the
`AllowOutOfOrderMetadataProperties` is set to `true` in your `JsonSourceGenerationOptions` on your `SourceGenerationContext` class. For example,

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

`idunno.AtProto`, `idunno.Bluesky`, the non-UI ASP.NET authentication packages, and the BotSdk packages (including their storage providers) support
[.NET native ahead-of-time (AOT)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).

`idunno.Bluesky.AspNet.Authentication.UI` contains Razor Pages and does not support trimming or Native AOT. Its
`AddBlueskyAuthenticationUI()` entry point is annotated with `RequiresUnreferencedCode` and `RequiresDynamicCode`.

Native AOT has benefits:

* Reduced disk footprint: Native AOT publishing produces a single executable containing just the code from external dependencies that is used by your application.
* Reduced startup time: Native AOT can show reduced start-up times, meaning your app is ready to respond more quickly.
* Reduced memory usage: Native AOT apps can show reduced memory demands, depending on the work it does.

If you wish to use AOT with your own classes you must enable JSON Source Generation for your `AtProtoRecordValue` and `AtProtoRecord` records and follow the guidance above.

# Trimming

Trimming is supported if your application targets .NET 9.0 or later. You cannot target .NET 8.0 due the use of v9 of `System.Text.Json` and
a [bug in the linker](https://github.com/dotnet/runtime/issues/114307).
