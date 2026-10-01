// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text.Json;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures the LINQ queries which run once per jetstream event and once per request, against loop based replacements
/// which return the same results.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LinqCollectionBenchmarks
{
    private Dictionary<string, JsonElement> _extensionData = null!;
    private List<NameValueHeaderValue> _requestHeaders = null!;
    private List<NameValueHeaderValue> _extraRequestHeaders = null!;

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
        _extraRequestHeaders = [new NameValueHeaderValue("atproto-accept-labelers", "did:plc:ar7c4by46qjdydhdevvrndac"), new NameValueHeaderValue("user-agent-extra",         "benchmarks")];

                if (!ExtensionDataExceptCurrent(_extensionData, "commit").Keys.SequenceEqual(ExtensionDataExceptProposed(_extensionData, "commit").Keys) ||
            !MergeRequestHeadersCurrent(_requestHeaders, _extraRequestHeaders).Select(h => h.Name).SequenceEqual(MergeRequestHeadersProposed(_requestHeaders, _extraRequestHeaders).Select(h => h.Name)))
        {
            throw new InvalidOperationException("The proposed replacements return different results.");
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ExtensionDataExcept")]
    public int ExtensionDataExceptWhere() => ExtensionDataExceptCurrent(_extensionData, "commit").Count;

    [Benchmark]
    [BenchmarkCategory("ExtensionDataExcept")]
    public int ExtensionDataExceptLoop() => ExtensionDataExceptProposed(_extensionData, "commit").Count;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MergeRequestHeaders")]
    public int MergeRequestHeadersAny() => MergeRequestHeadersCurrent(_requestHeaders, _extraRequestHeaders).Count;

    [Benchmark]
    [BenchmarkCategory("MergeRequestHeaders")]
    public int MergeRequestHeadersLoop() => MergeRequestHeadersProposed(_requestHeaders, _extraRequestHeaders).Count;

    // A copy of AtProtoJetStream.ExtensionDataExcept.
    private static Dictionary<string, JsonElement> ExtensionDataExceptCurrent(IDictionary<string, JsonElement> extensionData, string consumedKey)
    {
        Dictionary<string, JsonElement> remaining = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonElement> entry in extensionData.Where(entry => !string.Equals(entry.Key, consumedKey, StringComparison.Ordinal)))
        {
            remaining.Add(entry.Key, entry.Value);
        }

        return remaining;
    }

    private static Dictionary<string, JsonElement> ExtensionDataExceptProposed(IDictionary<string, JsonElement> extensionData, string consumedKey)
    {
        Dictionary<string, JsonElement> remaining = new(extensionData.Count, StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonElement> entry in extensionData)
        {
            if (!string.Equals(entry.Key, consumedKey, StringComparison.Ordinal))
            {
                remaining.Add(entry.Key, entry.Value);
            }
        }

        return remaining;
    }

    // A copy of AtProtoHttpClient.MergeRequestHeaders.
    private static ICollection<NameValueHeaderValue> MergeRequestHeadersCurrent(ICollection<NameValueHeaderValue> requestHeaders, List<NameValueHeaderValue> extraRequestHeaders)
    {
        List<NameValueHeaderValue> mergedHeaders = [.. requestHeaders];

        foreach (NameValueHeaderValue header in extraRequestHeaders)
        {
            if (mergedHeaders.Any(h => h.Name.Equals(header.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            mergedHeaders.Add(header);
        }

        return mergedHeaders;
    }

    private static ICollection<NameValueHeaderValue> MergeRequestHeadersProposed(ICollection<NameValueHeaderValue> requestHeaders, List<NameValueHeaderValue> extraRequestHeaders)
    {
        List<NameValueHeaderValue> mergedHeaders = new(requestHeaders.Count + extraRequestHeaders.Count);
        mergedHeaders.AddRange(requestHeaders);
        int supplied = mergedHeaders.Count;

        foreach (NameValueHeaderValue header in extraRequestHeaders)
        {
            bool present = false;
            for (int i = 0; i < supplied; i++)
            {
                if (mergedHeaders[i].Name.Equals(header.Name, StringComparison.OrdinalIgnoreCase))
                {
                    present = true;
                    break;
                }
            }

            if (!present)
            {
                mergedHeaders.Add(header);
            }
        }

        return mergedHeaders;
    }

}