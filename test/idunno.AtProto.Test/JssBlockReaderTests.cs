// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;

using ZstdSharp;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class JssBlockReaderTests
{
    private static readonly byte[] s_record = Convert.FromHexString("A164746573746178");
    private const string TestDid = "did:plc:g6ylltenitt4tp27bpwalh7b";
    private const string ExpectedCid = "bafyreiahoxao3topglxgvzelhwm6ldqudzmtvtljnlnyndtfvotpvgf6zm";

    [Theory]
    [InlineData(1, JetstreamCommitOperation.Create, false)]
    [InlineData(2, JetstreamCommitOperation.Update, false)]
    [InlineData(7, JetstreamCommitOperation.Create, true)]
    public void RecordRowsHaveAnOwnedLazyCid(byte kind, JetstreamCommitOperation operation, bool backfill)
    {
        JssRow row = Assert.Single(JssBlockReader.Decode(Block(kind, s_record)));
        JetstreamCommitEvent evt = Assert.IsType<JetstreamCommitEvent>(row.ToEvent());
        Assert.Equal(operation, evt.Commit.Operation);
        Assert.Equal(backfill, evt.IsSyncBackfill);
        Assert.Equal(42, evt.Sequence);
        Assert.Equal(123456, evt.TimeStamp);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddTicks(1234560), evt.WitnessedAt);
        Assert.Equal("x", evt.Commit.Record?.GetProperty("test").GetString());

#pragma warning disable CS0618 // Verify the compatibility conversion preserves the archive's deferred CID.
        JetstreamCommit converted = evt.Commit;
        Assert.False(converted.DeferredCid?.IsValueCreated);
#pragma warning restore CS0618
        Array.Fill(row.Payload, (byte)0);
        Assert.Equal(ExpectedCid, evt.Commit.Cid?.ToString());
        Assert.Same(evt.Commit.Cid, evt.Commit.Cid);
        Assert.Equal(ExpectedCid, converted.Cid?.ToString());
        Assert.Same(converted.Cid, converted.Cid);
        Assert.Same(evt.Commit.Cid, converted.Cid);
    }

    [Fact]
    public void DeleteRowsDoNotAcquireACid()
    {
        JssRow row = Assert.Single(JssBlockReader.Decode(Block(3, [])));
        JetstreamCommitEvent evt = Assert.IsType<JetstreamCommitEvent>(row.ToEvent());
        Assert.Equal(JetstreamCommitOperation.Delete, evt.Commit.Operation);
        Assert.Null(evt.Commit.Record);
        Assert.Null(evt.Commit.Cid);
    }

    [Fact]
    public void FutureKindsAreDeliveredAsUnknown()
    {
        JssRow row = Assert.Single(JssBlockReader.Decode(Block(99, [])));
        JetstreamEvent evt = Assert.IsType<JetstreamEvent>(row.ToEvent());
        Assert.Equal(JetStreamEventKind.Unknown, evt.Kind);
        Assert.Equal(42, evt.Sequence);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void MetadataRowsKeepTheirUpstreamSequenceSeparateFromTheArchiveCursor(byte kind)
    {
        CborWriter writer = new();
        writer.WriteStartMap(kind == 4 ? 3 : 4);
        writer.WriteTextString("did");
        writer.WriteTextString(TestDid);
        if (kind == 6)
        {
            writer.WriteTextString("rev");
            writer.WriteTextString("3mpksbjhx5s26");
        }

        writer.WriteTextString("seq");
        writer.WriteUInt64(33_900_000_000);
        writer.WriteTextString("time");
        writer.WriteTextString("2026-09-26T00:19:35Z");
        if (kind == 5)
        {
            writer.WriteTextString("active");
            writer.WriteBoolean(true);
        }
        writer.WriteEndMap();
        JetstreamEvent evt = Assert.Single(JssBlockReader.Decode(Block(kind, writer.Encode()))).ToEvent();
        Assert.Equal(42, evt.Sequence);
        switch (kind)
        {
            case 4:
                Assert.Equal(33_900_000_000UL, Assert.IsType<JetstreamIdentityEvent>(evt).Identity.Sequence);
                break;
            case 5:
                JetstreamAccountEvent account = Assert.IsType<JetstreamAccountEvent>(evt);
                Assert.Equal(33_900_000_000UL, account.Account.Sequence);
                Assert.True(account.Account.Active);
                break;
            case 6:
                Assert.Equal(33_900_000_000, Assert.IsType<JetstreamSyncEvent>(evt).Sync.Sequence);
                break;
        }
    }

    [Fact]
    public void CapturedArchiveLikesDecodeWithTheirOriginalContentIdentifiers()
    {
        using Stream fixture = typeof(JssBlockReaderTests).Assembly.GetManifestResourceStream(
            "idunno.AtProto.Test.Fixtures.JetstreamArchiveLikes.json")
            ?? throw new InvalidOperationException("The captured Jetstream fixture is missing.");
        using JsonDocument capture = JsonDocument.Parse(fixture);
        Assert.Equal(2, capture.RootElement.GetProperty("rows").GetArrayLength());
        foreach (JsonElement sample in capture.RootElement.GetProperty("rows").EnumerateArray())
        {
            long seq = sample.GetProperty("seq").GetInt64();
            long witnessed = sample.GetProperty("witnessedAt").GetInt64();
            string did = Assert.IsType<string>(sample.GetProperty("did").GetString());
            string collection = Assert.IsType<string>(sample.GetProperty("collection").GetString());
            string rkey = Assert.IsType<string>(sample.GetProperty("rkey").GetString());
            string rev = Assert.IsType<string>(sample.GetProperty("rev").GetString());
            byte[] payload = Convert.FromHexString(
                Assert.IsType<string>(sample.GetProperty("cbor").GetString()));
            JssRow row = Assert.Single(JssBlockReader.Decode(Block(
                kind: sample.GetProperty("kind").GetByte(), payload: payload, seq: seq,
                witnessedAt: witnessed, indexedAt: sample.GetProperty("indexedAt").GetInt64(),
                didValue: did, collectionValue: collection, rkeyValue: rkey, revValue: rev)));
            JetstreamCommitEvent evt = Assert.IsType<JetstreamCommitEvent>(row.ToEvent());

            Assert.Equal(seq, evt.Sequence);
            Assert.Equal(did, evt.Did.Value);
            Assert.Equal(collection, evt.Commit.Collection.ToString());
            Assert.Equal(rkey, evt.Commit.RKey.ToString());
            Assert.Equal(rev, evt.Commit.Rev);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddTicks(checked(witnessed * 10)), evt.WitnessedAt);
            Assert.Equal(Assert.IsType<string>(sample.GetProperty("cid").GetString()), evt.Commit.Cid?.ToString());
        }
    }

    [Fact]
    public void TruncatedColumnarDataIsRejected()
    {
        using Compressor compressor = new();
        byte[] frame = compressor.Wrap([1, 0, 0, 0]).ToArray();
        Assert.Throws<InvalidDataException>(() => JssBlockReader.Decode(frame));
    }

    private static byte[] Block(
        byte kind, byte[] payload, long seq = 42, long witnessedAt = 123456, long indexedAt = 0,
        string didValue = TestDid, string collectionValue = "app.bsky.feed.like",
        string rkeyValue = "3mfrqvim56e25", string revValue = "3mpksbjhx5s26")
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        byte[] did = Encoding.UTF8.GetBytes(didValue);
        bool commit = kind is 1 or 2 or 3 or 7;
        byte[] collection = commit ? Encoding.UTF8.GetBytes(collectionValue) : [];
        byte[] rkey = commit ? Encoding.UTF8.GetBytes(rkeyValue) : [];
        byte[] rev = commit ? Encoding.UTF8.GetBytes(revValue) : [];
        writer.Write(1U);
        writer.Write(checked((ulong)seq));
        writer.Write(witnessedAt);
        writer.Write(indexedAt);
        writer.Write(kind);
        writer.Write(checked((byte)collection.Length));
        writer.Write(checked((ushort)did.Length));
        writer.Write(checked((byte)rkey.Length));
        writer.Write(checked((byte)rev.Length));
        writer.Write(checked((uint)payload.Length));
        writer.Write(collection);
        writer.Write(did);
        writer.Write(rkey);
        writer.Write(rev);
        writer.Write(payload);
        using Compressor compressor = new();
        return compressor.Wrap(stream.ToArray()).ToArray();
    }
}
