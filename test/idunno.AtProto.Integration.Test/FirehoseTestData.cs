// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections;
using System.Formats.Cbor;
using System.Security.Cryptography;

namespace idunno.AtProto.Integration.Test;

/// <summary>
/// Builds DAG-CBOR event stream frames, CAR files and signed commits for the firehose tests.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class FirehoseTestData
{
    public const string TestDid = "did:plc:ewvi7nxzyoun6zhxsrcy6jgr";
    public const string OtherDid = "did:plc:g6ylltenitt4tp27bpwalh7b";
    public const string Revision = "3lngovum7vm2k";
    public const string OtherRevision = "3lngovum7vm2l";
    public const string TestRecordKey = "3lngovum7vm2k";
    public const string Time = "2026-09-28T01:02:03.000Z";

    public static Dictionary<string, object?> Map(params (string Key, object? Value)[] fields)
    {
        Dictionary<string, object?> map = new(StringComparer.Ordinal);

        foreach ((string key, object? value) in fields)
        {
            map[key] = value;
        }

        return map;
    }

    public static byte[] Encode(object? value)
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        Write(writer, value);
        return writer.Encode();
    }

    public static Cid CidOf(byte[] data) => new([0x01, 0x71, 0x12, 0x20, .. SHA256.HashData(data)]);

    public static byte[] Frame(string? type, object? payload, long operation = 1)
    {
        Dictionary<string, object?> header = type is null ? Map(("op", operation)) : Map(("op", operation), ("t", type));
        return [.. Encode(header), .. Encode(payload)];
    }

    public static byte[] ErrorFrame(string error, string? message = null) =>
        Frame(null, message is null ? Map(("error", error)) : Map(("error", error), ("message", message)), operation: -1);

    public static byte[] Car(Cid root, params byte[][] blocks) => Car(root, [.. blocks.Select(block => (CidOf(block), block))]);

    public static byte[] Car(Cid root, (Cid Cid, byte[] Data)[] blocks)
    {
        using MemoryStream stream = new();
        WriteSection(stream, Encode(Map(("roots", new object?[] { root }), ("version", 1L))));

        foreach ((Cid cid, byte[] data) in blocks)
        {
            WriteSection(stream, [.. cid.ToBytes(), .. data]);
        }

        return stream.ToArray();
    }

    public static byte[] RecordBytes(string text = "hello") =>
        Encode(Map(("$type", "app.bsky.feed.post"), ("text", text), ("createdAt", Time)));

    public static byte[] UnsignedCommit(string did, string rev, Cid data) =>
        Encode(Map(("did", did), ("version", 3L), ("prev", null), ("data", data), ("rev", rev)));

    public static byte[] SignedCommit(string did, string rev, Cid data, byte[] signature) =>
        Encode(Map(("did", did), ("version", 3L), ("prev", null), ("data", data), ("rev", rev), ("sig", signature)));

    public static byte[] Sign(ECDsa key, byte[] data) => key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public static DidDocument DidDocumentFor(string did, string fragment, ECDsa key)
    {
        ECPoint point = key.ExportParameters(false).Q;
        byte[] compressed = [(point.Y![^1] & 1) == 1 ? (byte)0x03 : (byte)0x02, .. point.X!];
        string multibaseKey = $"z{SimpleBase.Base58.Bitcoin.Encode([0x80, 0x24, .. compressed])}";

        return new DidDocument(
            new Did(did),
            context: null,
            alsoKnownAs: null,
            verificationMethods:
            [
                new VerificationMethod($"{did}#{fragment}", "Multikey", new Did(did))
                {
                    PublicKeyMultibase = multibaseKey
                }
            ],
            services: null);
    }

    public static Dictionary<string, object?> IdentityPayload(long sequence, string did = TestDid, string? handle = "alice.test") =>
        handle is null
            ? Map(("seq", sequence), ("did", did), ("time", Time))
            : Map(("seq", sequence), ("did", did), ("time", Time), ("handle", handle));

    public static Dictionary<string, object?> AccountPayload(long sequence, bool active, string? status) =>
        status is null
            ? Map(("seq", sequence), ("did", TestDid), ("time", Time), ("active", active))
            : Map(("seq", sequence), ("did", TestDid), ("time", Time), ("active", active), ("status", status));

    public static Dictionary<string, object?> LabelPayload(string value = "spam", ECDsa? key = null, bool corruptSignature = false, string source = OtherDid)
    {
        Dictionary<string, object?> label = Map(
            ("ver", 1L),
            ("src", source),
            ("uri", $"at://{TestDid}/app.bsky.feed.post/{TestRecordKey}"),
            ("cid", CidOf(RecordBytes()).ToString()),
            ("val", value),
            ("neg", false),
            ("cts", Time),
            ("exp", "2027-01-01T00:00:00.000Z"));

        byte[] signature = key is null ? new byte[64] : Sign(key, Encode(label));

        if (corruptSignature)
        {
            signature[0] ^= 1;
        }

        label["sig"] = signature;
        return label;
    }

    public static Dictionary<string, object?> LabelsPayload(long sequence, params Dictionary<string, object?>[] labels) =>
        Map(("seq", sequence), ("labels", labels.Cast<object?>().ToArray()));

    public static Dictionary<string, object?> InfoPayload(string name, string? message = null) =>
        message is null ? Map(("name", name)) : Map(("name", name), ("message", message));

    private static void WriteSection(Stream stream, byte[] section)
    {
        ulong length = (ulong)section.Length;

        do
        {
            byte value = (byte)(length & 0x7F);
            length >>= 7;
            stream.WriteByte(length == 0 ? value : (byte)(value | 0x80));
        }
        while (length != 0);

        stream.Write(section);
    }

    private static void Write(CborWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull();
                break;

            case bool boolean:
                writer.WriteBoolean(boolean);
                break;

            case int integer:
                writer.WriteInt64(integer);
                break;

            case long integer:
                writer.WriteInt64(integer);
                break;

            case string text:
                writer.WriteTextString(text);
                break;

            case byte[] bytes:
                writer.WriteByteString(bytes);
                break;

            case Cid cid:
                writer.WriteTag((CborTag)42);
                writer.WriteByteString([0x00, .. cid.ToBytes()]);
                break;

            case RawCbor raw:
                writer.WriteEncodedValue(raw.Encoded);
                break;

            case IDictionary<string, object?> map:
                writer.WriteStartMap(map.Count);
                foreach (KeyValuePair<string, object?> field in map)
                {
                    writer.WriteTextString(field.Key);
                    Write(writer, field.Value);
                }

                writer.WriteEndMap();
                break;

            case IEnumerable items:
                List<object?> list = [.. items.Cast<object?>()];
                writer.WriteStartArray(list.Count);
                foreach (object? item in list)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;

            default:
                throw new ArgumentException($"Cannot encode a {value.GetType()}.", nameof(value));
        }
    }
}

/// <summary>
/// Already encoded CBOR, written as it is.
/// </summary>
/// <param name="Encoded">The encoded value.</param>
[ExcludeFromCodeCoverage]
internal sealed record RawCbor(byte[] Encoded);

/// <summary>
/// Builds a <c>#commit</c> payload, with a CAR containing the commit block and record blocks, which each test can then break.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class TestCommit
{
    public TestCommit(long sequence)
    {
        Sequence = sequence;
        Operations.Add(FirehoseTestData.Map(
            ("action", "create"),
            ("path", $"app.bsky.feed.post/{FirehoseTestData.TestRecordKey}"),
            ("cid", RecordCid)));
    }

    public long Sequence { get; }

    public string Repo { get; set; } = FirehoseTestData.TestDid;

    public string CommitDid { get; set; } = FirehoseTestData.TestDid;

    public string Rev { get; set; } = FirehoseTestData.Revision;

    public string CommitRev { get; set; } = FirehoseTestData.Revision;

    public byte[] Record { get; } = FirehoseTestData.RecordBytes();

    public Cid RecordCid => FirehoseTestData.CidOf(Record);

    public Cid Data { get; } = FirehoseTestData.CidOf(FirehoseTestData.Encode(FirehoseTestData.Map()));

    public List<Dictionary<string, object?>> Operations { get; } = [];

    public List<(Cid Cid, byte[] Data)> Blocks { get; } = [];

    public bool IncludeRecordBlock { get; set; } = true;

    public Cid? Root { get; set; }

    public ECDsa? SigningKey { get; set; }

    public bool CorruptSignature { get; set; }

    public Action<Dictionary<string, object?>>? Configure { get; set; }

    public Dictionary<string, object?> Build()
    {
        byte[] signature = SigningKey is null
            ? new byte[64]
            : FirehoseTestData.Sign(SigningKey, FirehoseTestData.UnsignedCommit(CommitDid, CommitRev, Data));

        if (CorruptSignature)
        {
            signature[0] ^= 1;
        }

        byte[] commitBlock = FirehoseTestData.SignedCommit(CommitDid, CommitRev, Data, signature);
        Cid commit = FirehoseTestData.CidOf(commitBlock);

        // The live relay does not put the commit block first, so neither does the test.
        List<(Cid Cid, byte[] Data)> blocks = [];

        if (IncludeRecordBlock)
        {
            blocks.Add((RecordCid, Record));
        }

        blocks.Add((commit, commitBlock));
        blocks.AddRange(Blocks);

        Dictionary<string, object?> payload = FirehoseTestData.Map(
            ("seq", Sequence),
            ("rebase", false),
            ("tooBig", false),
            ("repo", Repo),
            ("commit", commit),
            ("rev", Rev),
            ("since", null),
            ("blocks", FirehoseTestData.Car(Root ?? commit, [.. blocks])),
            ("ops", Operations.Cast<object?>().ToArray()),
            ("blobs", Array.Empty<object?>()),
            ("prevData", Data),
            ("time", FirehoseTestData.Time));

        Configure?.Invoke(payload);

        return payload;
    }
}
