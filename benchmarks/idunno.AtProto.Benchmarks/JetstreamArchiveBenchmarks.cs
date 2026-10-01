// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Text;

using BenchmarkDotNet.Attributes;
using ZstdSharp;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures decoding a jetstream archive block, per row, from synthetic blocks of like commits and of identity events.
/// </summary>
[MemoryDiagnoser]
public class JetstreamArchiveBenchmarks
{
    private const int RowCount = 1000;

    private byte[] _block = null!;
    private byte[] _identityBlock = null!;

    [GlobalSetup]
    public void Setup()
    {
        _block = BuildBlock(1, LikeRecord());
        _identityBlock = BuildBlock(4, IdentityPayload());
    }

    [Benchmark(OperationsPerInvoke = RowCount)]
    public int Decode() => JssBlockReader.Decode(_block).Count;

    [Benchmark(OperationsPerInvoke = RowCount)]
    public int DecodeToEvents()
    {
        int total = 0;
        foreach (JssRow row in JssBlockReader.Decode(_block))
        {
            JetstreamEvent decoded = row.ToEvent();
            total += decoded.Kind == JetStreamEventKind.Commit ? 1 : 0;
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RowCount)]
    public int DecodeIdentitiesToEvents()
    {
        int total = 0;
        foreach (JssRow row in JssBlockReader.Decode(_identityBlock))
        {
            JetstreamEvent decoded = row.ToEvent();
            total += decoded.Kind == JetStreamEventKind.Identity ? 1 : 0;
        }

        return total;
    }

    private static byte[] BuildBlock(byte kind, byte[] payload)
    {
        bool isCommit = kind is 1 or 2 or 3 or 7;
        byte[] did = Encoding.UTF8.GetBytes("did:plc:ewvi7nxzyoun6zhxrhs64oiz");
        byte[] collection = isCommit ? Encoding.UTF8.GetBytes("app.bsky.feed.like") : [];
        byte[] rkey = isCommit ? Encoding.UTF8.GetBytes("3mfrqvim56e25") : [];
        byte[] rev = isCommit ? Encoding.UTF8.GetBytes("3mpksbjhx5s26") : [];

        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((uint)RowCount);
            WriteColumn(writer, i => writer.Write((long)(1000 + i)));
            WriteColumn(writer, i => writer.Write(1_700_000_000_000_000L + i));
            WriteColumn(writer, _ => writer.Write(0L));
            WriteColumn(writer, _ => writer.Write(kind));
            WriteColumn(writer, _ => writer.Write((byte)collection.Length));
            WriteColumn(writer, _ => writer.Write((ushort)did.Length));
            WriteColumn(writer, _ => writer.Write((byte)rkey.Length));
            WriteColumn(writer, _ => writer.Write((byte)rev.Length));
            WriteColumn(writer, _ => writer.Write((uint)payload.Length));
            WriteColumn(writer, _ => writer.Write(collection));
            WriteColumn(writer, _ => writer.Write(did));
            WriteColumn(writer, _ => writer.Write(rkey));
            WriteColumn(writer, _ => writer.Write(rev));
            WriteColumn(writer, _ => writer.Write(payload));
        }

        using Compressor compressor = new();
        return compressor.Wrap(stream.ToArray()).ToArray();
    }

    private static void WriteColumn(BinaryWriter writer, Action<int> write)
    {
        for (int i = 0; i < RowCount; i++)
        {
            write(i);
        }

        writer.Flush();
    }

    private static byte[] LikeRecord()
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        writer.WriteStartMap(3);
        writer.WriteTextString("$type");
        writer.WriteTextString("app.bsky.feed.like");
        writer.WriteTextString("subject");
        writer.WriteStartMap(2);
        writer.WriteTextString("cid");
        writer.WriteTextString("bafyreidfayvfuwqa7qlnopdjiqrxzs6blmoeu4rujcjtnci5beludirz2a");
        writer.WriteTextString("uri");
        writer.WriteTextString("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.post/3l3qo2vuowo2b");
        writer.WriteEndMap();
        writer.WriteTextString("createdAt");
        writer.WriteTextString("2026-01-01T00:00:00.000Z");
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static byte[] IdentityPayload()
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        writer.WriteStartMap(4);
        writer.WriteTextString("did");
        writer.WriteTextString("did:plc:ewvi7nxzyoun6zhxrhs64oiz");
        writer.WriteTextString("seq");
        writer.WriteUInt64(33_900_000_000);
        writer.WriteTextString("time");
        writer.WriteTextString("2026-09-26T00:19:35.000Z");
        writer.WriteTextString("handle");
        writer.WriteTextString("example.bsky.social");
        writer.WriteEndMap();
        return writer.Encode();
    }
}
