// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using idunno.AtProto.Json;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures <c>idunno.AtProto.Types</c> parsing, equality and identifier generation over values taken from the jetstream and XRPC corpora.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TypesBenchmarks
{
    private const int Count = CorpusFile.JetstreamMessageCount;

    private string[] _dids = null!;
    private string[] _handles = null!;
    private string[] _collections = null!;
    private string[] _recordKeys = null!;
    private string[] _atUris = null!;
    private byte[][] _didJson = null!;

    private Nsid[] _nsids = null!;
    private Nsid[] _nsidCopies = null!;

    private TimestampIdentifier[] _tids = null!;

    private readonly DidConverter _didConverter = new();
    private readonly JsonSerializerOptions _options = new();

    [GlobalSetup]
    public void Setup()
    {
        List<string> dids = [];
        List<string> collections = [];
        List<string> recordKeys = [];

        foreach (byte[] message in CorpusFile.Read(CorpusFile.JetstreamV1))
        {
            using JsonDocument document = JsonDocument.Parse(message);
            JsonElement root = document.RootElement;
            string did = root.GetProperty("did").GetString()!;
            dids.Add(did);

            if (root.TryGetProperty("commit", out JsonElement commit))
            {
                collections.Add(commit.GetProperty("collection").GetString()!);
                recordKeys.Add(commit.GetProperty("rkey").GetString()!);
            }
        }

        List<string> handles = [];
        foreach (string corpus in new[] { CorpusFile.XrpcGetProfiles, CorpusFile.XrpcGetTimeline, CorpusFile.XrpcGetAuthorFeed })
        {
            foreach (byte[] response in CorpusFile.Read(corpus))
            {
                using JsonDocument document = JsonDocument.Parse(response);
                CollectHandles(document.RootElement, handles);
            }
        }

        _dids = Fill(dids);
        _handles = Fill(handles);
        _collections = Fill(collections);
        _recordKeys = Fill(recordKeys);

        _atUris = new string[Count];
        for (int i = 0; i < Count; i++)
        {
            _atUris[i] = $"at://{_dids[i]}/{_collections[i]}/{_recordKeys[i]}";
        }

        _didJson = [.. _dids.Select(d => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(d)))];

        // Equal values in distinct instances, built from distinct strings, so equality cannot take a reference shortcut.
        _nsids = [.. _collections.Select(c => new Nsid(c))];
        _nsidCopies = [.. _collections.Select(c => new Nsid(new string(c.AsSpan())))];

        _tids = new TimestampIdentifier[Count];
        for (int i = 0; i < Count; i++)
        {
            _tids[i] = TimestampIdentifier.Next();
        }
    }

    private static void CollectHandles(JsonElement element, List<string> handles)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("handle") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        handles.Add(property.Value.GetString()!);
                    }
                    else
                    {
                        CollectHandles(property.Value, handles);
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    CollectHandles(item, handles);
                }

                break;
        }
    }

    private static string[] Fill(List<string> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("The corpus contains no usable values.");
        }

        string[] result = new string[Count];
        for (int i = 0; i < Count; i++)
        {
            result[i] = values[i % values.Count];
        }

        return result;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("DidParse")]
    public int DidParse()
    {
        int accepted = 0;
        foreach (string s in _dids)
        {
            if (Did.TryParse(s, out _))
            {
                accepted++;
            }
        }

        return accepted;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("DidConstruct")]
    public int DidConstruct()
    {
        int length = 0;
        foreach (string s in _dids)
        {
            length += new Did(s).Method.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("DidJsonRead")]
    public int DidJsonRead()
    {
        int length = 0;
        foreach (byte[] json in _didJson)
        {
            Utf8JsonReader reader = new(json);
            reader.Read();
            length += _didConverter.Read(ref reader, typeof(Did), _options)!.Method.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("HandleParse")]
    public int HandleParse()
    {
        int accepted = 0;
        foreach (string s in _handles)
        {
            if (Handle.TryParse(s, out _))
            {
                accepted++;
            }
        }

        return accepted;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("NsidParse")]
    public int NsidParse()
    {
        int accepted = 0;
        foreach (string s in _collections)
        {
            if (Nsid.TryParse(s, out _))
            {
                accepted++;
            }
        }

        return accepted;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("NsidEquals")]
    public int NsidEquals()
    {
        int equal = 0;
        for (int i = 0; i < _nsids.Length; i++)
        {
            if (_nsids[i].Equals(_nsidCopies[i]))
            {
                equal++;
            }
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("NsidNameAuthority")]
    public int NsidNameAuthority()
    {
        int length = 0;
        foreach (Nsid nsid in _nsids)
        {
            length += nsid.Name.Length + nsid.Authority.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("RecordKeyParse")]
    public int RecordKeyParse()
    {
        int accepted = 0;
        foreach (string s in _recordKeys)
        {
            if (RecordKey.TryParse(s, out _))
            {
                accepted++;
            }
        }

        return accepted;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("AtUriParse")]
    public int AtUriParse()
    {
        int accepted = 0;
        foreach (string s in _atUris)
        {
            if (AtUri.TryParse(s, out _))
            {
                accepted++;
            }
        }

        return accepted;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("AtUriConstruct")]
    public int AtUriConstruct()
    {
        int length = 0;
        foreach (string s in _atUris)
        {
            length += new AtUri(s).AbsolutePath!.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("TidNext")]
    public int TidNext()
    {
        int length = 0;
        for (int i = 0; i < Count; i++)
        {
            length += TimestampIdentifier.Next().Value.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("TidNew")]
    public int TidNew()
    {
        int length = 0;
        for (int i = 0; i < Count; i++)
        {
            length += new TimestampIdentifier().Value.Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("TidToString")]
    public int TidToString()
    {
        int length = 0;
        foreach (TimestampIdentifier tid in _tids)
        {
            length += tid.ToString().Length;
        }

        return length;
    }
}
