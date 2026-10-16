// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text.Json;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures the collection copies which run once per jetstream event and once per request, and which used to be LINQ queries.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LinqCollectionBenchmarks
{
    private Dictionary<string, JsonElement> _extensionData = null!;
    private List<NameValueHeaderValue> _requestHeaders = null!;
    private AtProtoHttpClient<object> _client = null!;

    // V1 jetstream events carry one extension property, the payload of their kind. Four shows an event with unknown properties.
    [Params(1, 4)]
    public int ExtensionDataCount { get; set; } = 1;

    [GlobalSetup]
    public void Setup()
    {
        _extensionData = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        using (JsonDocument document = JsonDocument.Parse("{\"commit\":{},\"a\":1,\"b\":2,\"c\":3}"))
        {
            foreach (JsonProperty property in document.RootElement.EnumerateObject().Take(ExtensionDataCount))
            {
                _extensionData.Add(property.Name, property.Value.Clone());
            }
        }

        _requestHeaders = [new NameValueHeaderValue("atproto-proxy", "did:web:api.bsky.app#bsky_appview")];

        _client = new AtProtoHttpClient<object>(
            "did:web:api.bsky.app#bsky_appview",
            [
                new NameValueHeaderValue("atproto-accept-labelers", "did:plc:ar7c4by46qjdydhdevvrndac"),
                new NameValueHeaderValue("user-agent-extra", "benchmarks")
            ]);
    }

    [Benchmark]
    [BenchmarkCategory("ExtensionDataExcept")]
    public int ExtensionDataExcept() => AtProtoJetstream.ExtensionDataExcept(_extensionData, "commit").Count;

    [Benchmark]
    [BenchmarkCategory("MergeRequestHeaders")]
    public int MergeRequestHeaders() => _client.MergeRequestHeaders(_requestHeaders)!.Count;
}
