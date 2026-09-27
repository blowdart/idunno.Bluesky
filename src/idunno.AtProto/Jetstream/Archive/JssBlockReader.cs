// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

using ZstdSharp;

namespace idunno.AtProto.Jetstream.Archive;

internal static class JssBlockReader
{
    internal const int MaximumFrameSize = 16 * 1024 * 1024;
    private const int MaximumDecodedSize = 64 * 1024 * 1024;
    private static readonly UTF8Encoding s_utf8 = new(false, true);

    internal static IReadOnlyList<JssRow> Decode(ReadOnlySpan<byte> compressed)
    {
        using Decompressor decompressor = new();
        byte[] data = decompressor.Unwrap(compressed, MaximumDecodedSize).ToArray();
        if (data.Length < sizeof(uint))
        {
            throw new InvalidDataException("The Jetstream block has no event count.");
        }

        int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data));
        int headersLength;
        try
        {
            headersLength = checked(4 + count * (8 + 8 + 8 + 1 + 1 + 2 + 1 + 1 + 4));
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("The Jetstream block event count is invalid.", ex);
        }

        if (headersLength > data.Length)
        {
            throw new InvalidDataException("The Jetstream block columns are truncated.");
        }

        int pos = 4;
        long[] seqs = ReadInt64Column(data, count, ref pos);
        long[] witnessed = ReadInt64Column(data, count, ref pos);
        long[] indexed = ReadInt64Column(data, count, ref pos);
        byte[] kinds = data.AsSpan(pos, count).ToArray();
        pos += count;
        byte[] collectionLengths = data.AsSpan(pos, count).ToArray();
        pos += count;
        ushort[] didLengths = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            didLengths[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos, 2));
            pos += 2;
        }

        byte[] rkeyLengths = data.AsSpan(pos, count).ToArray();
        pos += count;
        byte[] revLengths = data.AsSpan(pos, count).ToArray();
        pos += count;
        uint[] payloadLengths = new uint[count];
        for (int i = 0; i < count; i++)
        {
            payloadLengths[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos, 4));
            pos += 4;
        }

        long collectionSize = collectionLengths.Sum(length => (long)length);
        long didSize = didLengths.Sum(length => (long)length);
        long rkeySize = rkeyLengths.Sum(length => (long)length);
        long revSize = revLengths.Sum(length => (long)length);
        long payloadSize = payloadLengths.Sum(length => (long)length);
        if (collectionSize + didSize + rkeySize + revSize + payloadSize != data.Length - pos)
        {
            throw new InvalidDataException("The Jetstream block variable-length regions do not match the column sizes.");
        }

        int collectionPos = pos;
        int didPos = checked(collectionPos + (int)collectionSize);
        int rkeyPos = checked(didPos + (int)didSize);
        int revPos = checked(rkeyPos + (int)rkeySize);
        int payloadPos = checked(revPos + (int)revSize);
        List<JssRow> rows = new(count);
        for (int i = 0; i < count; i++)
        {
            string collection = ReadString(data, ref collectionPos, collectionLengths[i]);
            string did = ReadString(data, ref didPos, didLengths[i]);
            string rkey = ReadString(data, ref rkeyPos, rkeyLengths[i]);
            string rev = ReadString(data, ref revPos, revLengths[i]);
            int payloadLength = checked((int)payloadLengths[i]);
            byte[] payload = data.AsSpan(payloadPos, payloadLength).ToArray();
            payloadPos += payloadLength;
            rows.Add(new JssRow(seqs[i], witnessed[i], indexed[i] == 0 ? witnessed[i] : indexed[i],
                kinds[i], did, collection, rkey, rev, payload));
        }

        return rows;
    }

    private static long[] ReadInt64Column(byte[] data, int count, ref int pos)
    {
        long[] column = new long[count];
        for (int i = 0; i < count; i++)
        {
            column[i] = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(pos, 8));
            pos += 8;
        }

        return column;
    }

    private static string ReadString(byte[] data, ref int pos, int length)
    {
        string result = s_utf8.GetString(data, pos, length);
        pos += length;
        return result;
    }
}

internal sealed record JssRow(
    long Seq,
    long WitnessedAt,
    long IndexedAt,
    byte Kind,
    string Did,
    string Collection,
    string Rkey,
    string Rev,
    byte[] Payload)
{
    internal JetStreamEventKind EventKind => Kind switch
    {
        1 or 2 or 3 or 7 => JetStreamEventKind.Commit,
        4 => JetStreamEventKind.Identity,
        5 => JetStreamEventKind.Account,
        6 => JetStreamEventKind.Sync,
        _ => JetStreamEventKind.Unknown
    };

    internal JetstreamEvent ToEvent()
    {
        Did did = new(Did);
        DateTimeOffset witnessed = DateTimeOffset.UnixEpoch.AddTicks(checked(WitnessedAt * 10));
        long stamp = IndexedAt;
        switch (Kind)
        {
            case 1 or 2 or 3 or 7:
                if (string.IsNullOrEmpty(Collection) || string.IsNullOrEmpty(Rkey) || string.IsNullOrEmpty(Rev) ||
                    (Kind == 3 ? Payload.Length != 0 : Payload.Length == 0))
                {
                    throw new InvalidDataException("The archive commit row is malformed.");
                }

                JetstreamCommit commit = new()
                {
                    Operation = Kind switch
                    {
                        2 => JetstreamCommitOperation.Update,
                        3 => JetstreamCommitOperation.Delete,
                        _ => JetstreamCommitOperation.Create
                    },
                    Collection = new Nsid(Collection),
                    RKey = new RecordKey(Rkey),
                    Rev = Rev,
                    Record = Kind == 3 ? null : DagCbor.ToJsonElement(Payload)
                };
                if (Kind != 3)
                {
                    commit.SetDagCborPayload(Payload);
                }

                return new JetstreamCommitEvent
                {
                    Did = did, Sequence = Seq, TimeStamp = stamp, WitnessedAt = witnessed,
                    Kind = JetStreamEventKind.Commit,
                    IsSyncBackfill = Kind == 7,
                    Commit = commit
                };
            case 4:
                return new JetstreamIdentityEvent
                {
                    Did = did, Sequence = Seq, TimeStamp = stamp, WitnessedAt = witnessed,
                    Kind = JetStreamEventKind.Identity,
                    Identity = Deserialize(Payload, SourceGenerationContext.Default.AtJetStreamIdentity)
                };
            case 5:
                return new JetstreamAccountEvent
                {
                    Did = did, Sequence = Seq, TimeStamp = stamp, WitnessedAt = witnessed,
                    Kind = JetStreamEventKind.Account,
                    Account = Deserialize(Payload, SourceGenerationContext.Default.AtJetstreamAccount)
                };
            case 6:
                return new JetstreamSyncEvent
                {
                    Did = did, Sequence = Seq, TimeStamp = stamp, WitnessedAt = witnessed,
                    Kind = JetStreamEventKind.Sync,
                    Sync = Deserialize(Payload, SourceGenerationContext.Default.AtJetstreamSync)
                };
            default:
                return new JetstreamEvent
                {
                    Did = did, Sequence = Seq, TimeStamp = stamp, WitnessedAt = witnessed,
                    Kind = JetStreamEventKind.Unknown
                };
        }
    }

    private static T Deserialize<T>(byte[] bytes, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class =>
        JsonSerializer.Deserialize(DagCbor.ToJsonElement(bytes), typeInfo) ??
        throw new JsonException("The archive event payload was empty.");
}
