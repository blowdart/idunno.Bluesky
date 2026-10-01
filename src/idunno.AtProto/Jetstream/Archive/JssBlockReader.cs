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
    private const uint MaximumRows = 8192;
    private static readonly UTF8Encoding s_utf8 = new(false, true);

    internal static IReadOnlyList<JssRow> Decode(ReadOnlySpan<byte> compressed)
    {
        using Decompressor decompressor = new();

        // The decompressed block is only read here, so it is used where it is rather than being copied.
        ReadOnlySpan<byte> data = decompressor.Unwrap(compressed, MaximumDecodedSize);
        if (data.Length < sizeof(uint))
        {
            throw new InvalidDataException("The Jetstream block has no event count.");
        }

        uint rowCount = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (rowCount > MaximumRows)
        {
            throw new InvalidDataException("The Jetstream block event count exceeds the supported limit.");
        }

        int count = (int)rowCount;
        int headersLength = 4 + count * (8 + 8 + 8 + 1 + 1 + 2 + 1 + 1 + 4);

        if (headersLength > data.Length)
        {
            throw new InvalidDataException("The Jetstream block columns are truncated.");
        }

        int seqsPos = 4;
        int witnessedPos = seqsPos + count * 8;
        int indexedPos = witnessedPos + count * 8;
        int kindsPos = indexedPos + count * 8;
        int collectionLengthsPos = kindsPos + count;
        int didLengthsPos = collectionLengthsPos + count;
        int rkeyLengthsPos = didLengthsPos + count * 2;
        int revLengthsPos = rkeyLengthsPos + count;
        int payloadLengthsPos = revLengthsPos + count;
        int pos = payloadLengthsPos + count * 4;

        long collectionSize = 0;
        long didSize = 0;
        long rkeySize = 0;
        long revSize = 0;
        long payloadSize = 0;
        for (int i = 0; i < count; i++)
        {
            collectionSize += data[collectionLengthsPos + i];
            didSize += BinaryPrimitives.ReadUInt16LittleEndian(data[(didLengthsPos + i * 2)..]);
            rkeySize += data[rkeyLengthsPos + i];
            revSize += data[revLengthsPos + i];
            payloadSize += BinaryPrimitives.ReadUInt32LittleEndian(data[(payloadLengthsPos + i * 4)..]);
        }

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
            long seq = BinaryPrimitives.ReadInt64LittleEndian(data[(seqsPos + i * 8)..]);
            long witnessed = BinaryPrimitives.ReadInt64LittleEndian(data[(witnessedPos + i * 8)..]);
            long indexed = BinaryPrimitives.ReadInt64LittleEndian(data[(indexedPos + i * 8)..]);
            string collection = ReadString(data, ref collectionPos, data[collectionLengthsPos + i]);
            string did = ReadString(data, ref didPos, BinaryPrimitives.ReadUInt16LittleEndian(data[(didLengthsPos + i * 2)..]));
            string rkey = ReadString(data, ref rkeyPos, data[rkeyLengthsPos + i]);
            string rev = ReadString(data, ref revPos, data[revLengthsPos + i]);
            int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data[(payloadLengthsPos + i * 4)..]));

            // Each payload is copied out, so an event which is kept does not keep the whole decompressed block alive.
            byte[] payload = data.Slice(payloadPos, payloadLength).ToArray();
            payloadPos += payloadLength;
            rows.Add(new JssRow(seq, witnessed, indexed == 0 ? witnessed : indexed,
                data[kindsPos + i], did, collection, rkey, rev, payload));
        }

        return rows;
    }

    private static string ReadString(ReadOnlySpan<byte> data, ref int pos, int length)
    {
        string result = s_utf8.GetString(data.Slice(pos, length));
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
                    Identity = Deserialize(Payload, SourceGenerationContext.Default.JetstreamIdentity)
                };
            case 5:
                return new JetstreamAccountEvent
                {
                    Did = did, Sequence = Seq, TimeStamp = stamp, WitnessedAt = witnessed,
                    Kind = JetStreamEventKind.Account,
                    Account = Deserialize(Payload, SourceGenerationContext.Default.JetstreamAccount)
                };
            case 6:
                return new JetstreamSyncEvent
                {
                    Did = did, Sequence = Seq, TimeStamp = stamp, WitnessedAt = witnessed,
                    Kind = JetStreamEventKind.Sync,
                    Sync = Deserialize(Payload, SourceGenerationContext.Default.JetstreamSync)
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
