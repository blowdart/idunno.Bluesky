// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures the <see cref="Cid"/> operations the firehose relies on.
/// </summary>
/// <remarks>
/// <para>Every firehose commit reads a CAR into a <see cref="Dictionary{TKey, TValue}"/> keyed by <see cref="Cid"/>, and then
/// looks each operation's block up in it, so <see cref="Cid.GetHashCode"/> runs at least twice per block.</para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CidBenchmarks
{
    private const int Count = 64;

    private byte[][] _contents = null!;
    private Cid[] _cids = null!;

    [GlobalSetup]
    public void Setup()
    {
        Random random = new(42);
        _contents = new byte[Count][];
        _cids = new Cid[Count];
        for (int i = 0; i < Count; i++)
        {
            _contents[i] = new byte[256];
            random.NextBytes(_contents[i]);
            _cids[i] = Cid.FromDagCbor(_contents[i]);
        }
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("CarBlockDictionary")]
    public int CarBlockDictionary()
    {
        Dictionary<Cid, int> blocks = new(Count);
        for (int i = 0; i < _cids.Length; i++)
        {
            blocks.TryAdd(_cids[i], i);
        }

        int found = 0;
        foreach (Cid cid in _cids)
        {
            if (blocks.TryGetValue(cid, out int value))
            {
                found += value;
            }
        }

        return found;
    }

    // A Cid caches its string form, so this measures reading the cached value.
    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("ToString")]
    public int CachedToString()
    {
        int length = 0;
        foreach (Cid cid in _cids)
        {
            length += cid.ToString().Length;
        }

        return length;
    }

    // Hashing a block and formatting the new Cid, which is what happens the first time a firehose block's Cid is used.
    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("CreateAndFormat")]
    public int CreateAndFormat()
    {
        int length = 0;
        foreach (byte[] content in _contents)
        {
            length += Cid.FromDagCbor(content).ToString().Length;
        }

        return length;
    }
}
