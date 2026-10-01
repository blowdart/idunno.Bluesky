// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures the <see cref="Cid"/> operations the firehose relies on, comparing the current implementation, the baseline,
/// with a proposed alternative which produces identical results.
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

    private Cid[] _cids = null!;

    [GlobalSetup]
    public void Setup()
    {
        Random random = new(42);
        _cids = new Cid[Count];
        byte[] content = new byte[256];
        for (int i = 0; i < Count; i++)
        {
            random.NextBytes(content);
            _cids[i] = Cid.FromDagCbor(content);
        }

        foreach (Cid cid in _cids)
        {
            if (ProposedToString(cid) != cid.ToString() || ProposedComparer.Instance.GetHashCode(cid) != cid.GetHashCode())
            {
                throw new InvalidOperationException("The proposed implementation does not match the current one.");
            }
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Count)]
    [BenchmarkCategory("CarBlockDictionary")]
    public int DictionaryCurrent() => FillAndLookUp(new Dictionary<Cid, int>(Count));

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("CarBlockDictionary")]
    public int DictionaryProposedHash() => FillAndLookUp(new Dictionary<Cid, int>(Count, ProposedComparer.Instance));

    [Benchmark(Baseline = true, OperationsPerInvoke = Count)]
    [BenchmarkCategory("ToString")]
    public int ToStringCurrent()
    {
        int length = 0;
        foreach (Cid cid in _cids)
        {
            length += cid.ToString().Length;
        }

        return length;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    [BenchmarkCategory("ToString")]
    public int ToStringProposed()
    {
        int length = 0;
        foreach (Cid cid in _cids)
        {
            length += ProposedToString(cid).Length;
        }

        return length;
    }

    private int FillAndLookUp(Dictionary<Cid, int> blocks)
    {
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

    // The hash is a byte[] whenever the Cid built it, so it can be hashed in place rather than copied.
    private sealed class ProposedComparer : IEqualityComparer<Cid>
    {
        public static ProposedComparer Instance { get; } = new();

        public bool Equals(Cid? x, Cid? y) => x == y;

        public int GetHashCode(Cid obj)
        {
            HashCode hashAlgorithm = default;
            hashAlgorithm.Add(obj.Version);
            hashAlgorithm.Add(obj.Codec);
            hashAlgorithm.AddBytes(obj.Hash is byte[] hash ? hash : [.. obj.Hash]);
            return hashAlgorithm.ToHashCode();
        }
    }

    // Writes the CID bytes into a stack buffer rather than a List<byte>, and lower cases while copying, rather than
    // allocating an upper case string, a lower case string and then the prefixed result.
    private static string ProposedToString(Cid cid)
    {
        byte[] hash = cid.Hash is byte[] array ? array : [.. cid.Hash];

        Span<byte> buffer = stackalloc byte[128];
        int length = 1 + VarIntLength(cid.Codec) + hash.Length;
        Span<byte> bytes = length <= buffer.Length ? buffer[..length] : new byte[length];

        bytes[0] = cid.Version;
        int offset = 1;
        ulong codec = cid.Codec;
        while (codec >= 0x80)
        {
            bytes[offset++] = (byte)(codec | 0x80);
            codec >>= 7;
        }

        bytes[offset++] = (byte)codec;
        hash.CopyTo(bytes[offset..]);

        string upper = SimpleBase.Base32.Rfc4648.Encode(bytes);

        return string.Create(upper.Length + 1, upper, static (destination, source) =>
        {
            destination[0] = 'b';
            for (int i = 0; i < source.Length; i++)
            {
                destination[i + 1] = char.ToLowerInvariant(source[i]);
            }
        });
    }

    private static int VarIntLength(ulong value)
    {
        int length = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            length++;
        }

        return length;
    }
}
