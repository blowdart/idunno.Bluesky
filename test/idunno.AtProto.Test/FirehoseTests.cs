// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Globalization;
using System.Text;

using idunno.AtProto.Firehose;
using idunno.AtProto.Repo;
using idunno.AtProto.Sync;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class FirehoseTests
{
    [Theory]
    [InlineData("wss://bsky.network", null, "wss://bsky.network/xrpc/com.atproto.sync.subscribeRepos")]
    [InlineData("https://relay.example:8443/some/path?x=1#frag", 42L, "wss://relay.example:8443/xrpc/com.atproto.sync.subscribeRepos?cursor=42")]
    [InlineData("http://localhost:2470", 0L, "ws://localhost:2470/xrpc/com.atproto.sync.subscribeRepos?cursor=0")]
    [InlineData("ws://localhost", 9_007_199_254_740_991L, "ws://localhost/xrpc/com.atproto.sync.subscribeRepos?cursor=9007199254740991")]
    public void BuildSubscriptionUriMapsSchemesAndAppendsTheCursor(string host, long? cursor, string expected)
    {
        Uri uri = EventStreamReader.BuildSubscriptionUri(new Uri(host), "com.atproto.sync.subscribeRepos", cursor);

        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("line\r\nbreak\u001b[0m\u0007", "linebreak[0m")]
    [InlineData("invoice\u202Efdp.exe", "invoicefdp.exe")]
    [InlineData("\u2066a\u2067b\u2068c\u2069\u202Ad\u202Be\u202Cf\u202Dg\u200Eh\u200Fi\u061Cj", "abcdefghij")]
    [InlineData("مرحبا hello", "مرحبا hello")]
    public void SanitizeStripsControlAndBidirectionalFormattingCharacters(string value, string expected) => Assert.Equal(expected, EventStreamReader.Sanitize(value));

    [Fact]
    public void SanitizeTruncatesLongText()
    {
        Assert.Equal(EventStreamReader.MaximumServerTextLength, EventStreamReader.Sanitize(new string('a', 5000)).Length);
        Assert.Null(EventStreamReader.Sanitize(null));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public void SanitizeDoesNotSplitSurrogatePairsWhenTruncating(int space, bool emojiKept)
    {
        string prefix = new('a', EventStreamReader.MaximumServerTextLength - space);
        string sanitized = EventStreamReader.Sanitize(prefix + "\U0001F600tail");

        Assert.Equal(emojiKept ? prefix + "\U0001F600" : prefix, sanitized);
    }

    [Fact]
    public void SanitizeReplacesUnpairedSurrogates() => Assert.Equal("a\uFFFDb", EventStreamReader.Sanitize("a\uD800b"));

    [Theory]
    [InlineData(null, new[] { "1a", "2a", "5a" }, new[] { "Accept", "Accept", "Accept" })]
    [InlineData(10L, new[] { "10a", "11a" }, new[] { "DropCursorEvent", "Accept" })]
    [InlineData(10L, new[] { "11a", "12a" }, new[] { "Accept", "Accept" })]
    [InlineData(10L, new[] { "10a", "10a" }, new[] { "DropCursorEvent", "DropRepeatedEvent" })]
    [InlineData(10L, new[] { "10a", "10b" }, new[] { "DropCursorEvent", "Violation" })]
    [InlineData(10L, new[] { "9a" }, new[] { "Violation" })]
    [InlineData(null, new[] { "5a", "5a" }, new[] { "Accept", "Violation" })]
    [InlineData(0L, new[] { "5a", "5a", "6a" }, new[] { "Accept", "DropRepeatedEvent", "Accept" })]
    [InlineData(0L, new[] { "5a", "5a", "5a" }, new[] { "Accept", "DropRepeatedEvent", "Violation" })]
    [InlineData(10L, new[] { "10a", "10a", "10a" }, new[] { "DropCursorEvent", "DropRepeatedEvent", "Violation" })]
    [InlineData(0L, new[] { "5a", "5a", "6a", "6a" }, new[] { "Accept", "DropRepeatedEvent", "Accept", "Violation" })]
    [InlineData(10L, new[] { "11a", "11a", "12a", "12a" }, new[] { "Accept", "DropRepeatedEvent", "Accept", "Violation" })]
    [InlineData(null, new[] { "5a", "5b" }, new[] { "Accept", "Violation" })]
    [InlineData(null, new[] { "5a", "5a#account" }, new[] { "Accept", "Violation" })]
    [InlineData(10L, new[] { "10a", "10a#account" }, new[] { "DropCursorEvent", "Violation" })]
    [InlineData(null, new[] { "5a", "6a", "5a" }, new[] { "Accept", "Accept", "Violation" })]
    [InlineData(null, new[] { "5a", "4a" }, new[] { "Accept", "Violation" })]
    public void SequenceStateRejectsDuplicatesAndOutOfOrderSequences(long? cursor, string[] frames, string[] expected)
    {
        EventStreamReader.SequenceState state = new(cursor ?? 0);
        state.BeginConnection(cursor);

        // Each frame is its sequence number, a letter which stands for its payload, and optionally a message type other than #identity.
        string[] decisions = [.. frames.Select(frame =>
        {
            string[] parts = frame.Split('#');
            string type = parts.Length > 1 ? "#" + parts[1] : "#identity";
            return state.Accept(long.Parse(parts[0][..^1], CultureInfo.InvariantCulture), type, Encoding.UTF8.GetBytes(parts[0])).ToString();
        })];

        Assert.Equal(expected, decisions);
    }

    [Theory]
    [InlineData("5", 5L)]
    [InlineData(" 5 ", 5L)]
    [InlineData("99999999999", 2147483647L)]
    [InlineData("-5", null)]
    [InlineData("-9223372036854775808", null)]
    [InlineData("+5", null)]
    [InlineData("5.5", null)]
    [InlineData("soon", null)]
    public void GetRetryAfterAcceptsOnlyNonNegativeDeltaSeconds(string value, long? expectedSeconds)
    {
        Dictionary<string, IEnumerable<string>> headers = new() { ["retry-after"] = [value] };

        Assert.Equal(expectedSeconds is long seconds ? TimeSpan.FromSeconds(seconds) : null, EventStreamReader.GetRetryAfter(headers));
    }

    [Fact]
    public void SequenceStateOnlyDropsTheFirstFrameOfEachConnection()
    {
        EventStreamReader.SequenceState state = new(0);
        state.BeginConnection(null);
        Assert.Equal(EventStreamReader.SequenceDecision.Accept, state.Accept(7, "#identity", "a"u8.ToArray()));

        state.BeginConnection(state.Last);

        Assert.Equal(EventStreamReader.SequenceDecision.DropCursorEvent, state.Accept(7, "#identity", "a"u8.ToArray()));
        Assert.Equal(EventStreamReader.SequenceDecision.Accept, state.Accept(8, "#identity", "b"u8.ToArray()));
        Assert.Equal(8, state.Last);
    }

    [Fact]
    public void SequenceStateAllowsOneRepeatOnEachResumedConnection()
    {
        EventStreamReader.SequenceState state = new(0);
        state.BeginConnection(0);
        Assert.Equal(EventStreamReader.SequenceDecision.Accept, state.Accept(7, "#identity", "a"u8.ToArray()));
        Assert.Equal(EventStreamReader.SequenceDecision.DropRepeatedEvent, state.Accept(7, "#identity", "a"u8.ToArray()));
        Assert.Equal(EventStreamReader.SequenceDecision.Accept, state.Accept(8, "#identity", "b"u8.ToArray()));

        state.BeginConnection(state.Last);
        Assert.Equal(EventStreamReader.SequenceDecision.Accept, state.Accept(9, "#identity", "c"u8.ToArray()));
        Assert.Equal(EventStreamReader.SequenceDecision.DropRepeatedEvent, state.Accept(9, "#identity", "c"u8.ToArray()));

        state.BeginConnection(null);
        Assert.Equal(EventStreamReader.SequenceDecision.Accept, state.Accept(10, "#identity", "d"u8.ToArray()));
        Assert.Equal(EventStreamReader.SequenceDecision.Violation, state.Accept(10, "#identity", "d"u8.ToArray()));
    }

    [Fact]
    public void FrameParseReadsTheHeaderAndPayload()
    {
        byte[] header = Encode(writer =>
        {
            writer.WriteStartMap(3);
            writer.WriteTextString("t");
            writer.WriteTextString("#commit");
            writer.WriteTextString("op");
            writer.WriteInt64(1);
            writer.WriteTextString("future");
            writer.WriteBoolean(true);
            writer.WriteEndMap();
        });
        byte[] payload = EncodeMap(("seq", 3L));

        FirehoseFrame frame = FirehoseFrame.Parse((byte[])[.. header, .. payload]);

        Assert.Equal(FirehoseFrame.MessageOperation, frame.Operation);
        Assert.Equal("#commit", frame.Type);
        Assert.Equal(payload, frame.Payload.ToArray());
    }

    public static TheoryData<byte[]> InvalidFrames =>
    [
        Array.Empty<byte>(),
        new byte[] { 0x1F },
        EncodeMap(("op", 1L), ("t", "#x")),
        (byte[])[.. EncodeMap(("op", 1L), ("t", "#x")), .. EncodeMap(("a", 1L)), .. EncodeMap(("a", 1L))],
        (byte[])[.. EncodeMap(("op", "1"), ("t", "#x")), .. EncodeMap(("a", 1L))],
        (byte[])[.. EncodeMap(("op", 1L)), .. EncodeMap(("a", 1L))],
        (byte[])[.. EncodeMap(("op", 1L), ("t", "#x")), 0x18, 0x01],
        (byte[])[.. EncodeMap(("op", 1L), ("t", "#x")), 0xF9, 0x3C, 0x00],
        (byte[])[.. EncodeMap(("op", 1L), ("t", "#x")), 0xF7],
        (byte[])[.. EncodeMap(("op", 1L), ("t", "#x")), 0xA1, 0x01, 0x01],
        (byte[])[.. EncodeMap(("op", 1L), ("t", "#x")), 0x5F, 0x41, 0x00, 0xFF],
        (byte[])[.. EncodeMap(("op", 1L), ("t", "#x")), 0x1B, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF],
        (byte[])[.. EncodeMap(("op", 1L), ("t", "#x")), 0xD8, 0x2A, 0x42, 0x01, 0x71],
    ];

    [Theory]
    [MemberData(nameof(InvalidFrames))]
    public void FrameParseRejectsInvalidFraming(byte[] message) => Assert.Throws<InvalidDataException>(() => FirehoseFrame.Parse(message));

    [Fact]
    public void FrameParseAllowsNestingUpToTheMaximumDepth()
    {
        byte[] header = EncodeMap(("op", 1L), ("t", "#x"));

        FirehoseFrame.Parse((byte[])[.. header, .. Nested(FirehoseCbor.MaximumDepth - 1)]);
        Assert.Throws<InvalidDataException>(() => FirehoseFrame.Parse((byte[])[.. header, .. Nested(FirehoseCbor.MaximumDepth + 1)]));
    }

    [Theory]
    [InlineData((byte)0x81)]
    [InlineData((byte)0xA1)]
    public void FrameParseRejectsDeepNestingWithoutAmplifyingMemory(byte container)
    {
        byte[] header = Encode(writer =>
        {
            writer.WriteStartMap(1);
            writer.WriteTextString("op");
            writer.WriteInt32(1);
            writer.WriteEndMap();
        });
        byte[] message = [.. header, .. DeeplyNested(container, 1024 * 1024)];

        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidDataException>(() => FirehoseFrame.Parse(message));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < message.Length, $"Parsing a {message.Length} byte frame allocated {allocated} bytes.");
    }

    [Theory]
    [InlineData((byte)0x81)]
    [InlineData((byte)0xA1)]
    public void ReadUnsignedCommitRejectsDeepNestingWithoutAmplifyingMemory(byte container)
    {
        // A commit declares six fields; the first is deeply nested so the reader reaches it before anything else fails.
        byte[] block = [0xA6, 0x61, (byte)'a', .. DeeplyNested(container, 1024 * 1024)];

        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidDataException>(() => CarReader.ReadUnsignedCommit(block));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < block.Length, $"Reading a {block.Length} byte commit allocated {allocated} bytes.");
    }
    [Fact]
    public void CborFieldsDistinguishAbsentFromNull()
    {
        CborFields fields = FirehoseCbor.ReadFields(Encode(writer =>
        {
            writer.WriteStartMap(1);
            writer.WriteTextString("cid");
            writer.WriteNull();
            writer.WriteEndMap();
        }), new CborFieldNames("cid", "prev"));

        Assert.True(fields.Contains("cid"));
        Assert.True(fields.IsAbsentOrNull("cid"));
        Assert.Null(fields.GetOptionalCidLink("cid"));
        Assert.False(fields.Contains("prev"));
        Assert.True(fields.IsAbsentOrNull("prev"));
        Assert.Throws<InvalidDataException>(() => fields.GetCidLink("cid"));
    }

    [Fact]
    public void CborFieldsBytesAreCopiedAndLimited()
    {
        byte[] encoded = EncodeMap(("blocks", new byte[] { 1, 2, 3 }));
        CborFields fields = FirehoseCbor.ReadFields(encoded, new CborFieldNames("blocks"));

        byte[] blocks = fields.GetBytes("blocks", 3);
        encoded.AsSpan().Clear();

        Assert.Equal([1, 2, 3], blocks);
        Assert.Throws<InvalidDataException>(() => FirehoseCbor.ReadFields(EncodeMap(("blocks", new byte[] { 1, 2, 3 })), new CborFieldNames("blocks")).GetBytes("blocks", 2));
    }

    [Theory]
    [InlineData("2026-09-28T01:02:03Z")]
    [InlineData("2026-09-28T01:02:03.123+01:00")]
    public void CborFieldsReadDateTimesAsUniversal(string value)
    {
        DateTimeOffset time = FirehoseCbor.ReadFields(EncodeMap(("time", value)), new CborFieldNames("time")).GetDateTime("time");

        Assert.Equal(TimeSpan.Zero, time.Offset);
    }

    [Theory]
    [InlineData("1985-04-12T23:20:50.123Z", "1985-04-12T23:20:50.1230000+00:00")]
    [InlineData("1985-04-12T23:20:50.123456Z", "1985-04-12T23:20:50.1234560+00:00")]
    [InlineData("1985-04-12T23:20:50.120000Z", "1985-04-12T23:20:50.1200000+00:00")]
    [InlineData("0001-01-01T00:00:00.000Z", "0001-01-01T00:00:00.0000000+00:00")]
    [InlineData("1985-04-12T23:20:50.12345678912345Z", "1985-04-12T23:20:50.1234567+00:00")]
    [InlineData("1985-04-12T23:20:50Z", "1985-04-12T23:20:50.0000000+00:00")]
    [InlineData("1985-04-12T23:20:50.0Z", "1985-04-12T23:20:50.0000000+00:00")]
    [InlineData("1985-04-12T23:20:50.123+00:00", "1985-04-12T23:20:50.1230000+00:00")]
    [InlineData("1985-04-12T23:20:50.123-07:00", "1985-04-13T06:20:50.1230000+00:00")]
    public void CborFieldsAcceptAtProtocolDateTimes(string value, string expected)
    {
        Assert.True(CborFields.TryParseDateTime(value, out DateTimeOffset result));
        Assert.Equal(expected, result.ToString("O", CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("1985-04-12")]
    [InlineData("1985-04-12T23:20Z")]
    [InlineData("1985-04-12T23:20:5Z")]
    [InlineData("1985-04-12T23:20:50.123")]
    [InlineData("+001985-04-12T23:20:50.123Z")]
    [InlineData("23:20:50.123Z")]
    [InlineData("-1985-04-12T23:20:50.123Z")]
    [InlineData("1985-4-12T23:20:50.123Z")]
    [InlineData("01985-04-12T23:20:50.123Z")]
    [InlineData("1985-04-12T23:20:50.123+00")]
    [InlineData("1985-04-12T23:20:50.123+0000")]
    [InlineData("1985-04-12t23:20:50.123Z")]
    [InlineData("1985-04-12T23:20:50.123z")]
    [InlineData("1985-04-12T23:20:50.123-00:00")]
    [InlineData("1985-04-12 23:20:50.123Z")]
    [InlineData("1985-04-12T23:99:50.123Z")]
    [InlineData("1985-00-12T23:20:50.123Z")]
    [InlineData("0000-01-01T00:00:00+01:00")]
    [InlineData("1985-04-12T23:20:50.Z")]
    [InlineData("1985-04-12T23:20:50.123Z ")]
    [InlineData("\u0661985-04-12T23:20:50.123Z")]
    [InlineData("04/12/1985 23:20:50")]
    [InlineData("Fri, 12 Apr 1985 23:20:50 GMT")]
    public void CborFieldsRejectInvalidAtProtocolDateTimes(string value)
    {
        Assert.False(CborFields.TryParseDateTime(value, out _));
        Assert.Throws<InvalidDataException>(() => FirehoseCbor.ReadFields(EncodeMap(("time", value)), new CborFieldNames("time")).GetDateTime("time"));
    }
    [Fact]
    public void CborFieldsRejectInvalidDids() =>
        Assert.Throws<InvalidDataException>(() => FirehoseCbor.ReadFields(EncodeMap(("did", "not a did")), new CborFieldNames("did")).GetDid("did"));

    [Fact]
    public void GetUnsignedLabelRemovesOnlyTheSignature()
    {
        byte[] unsigned = EncodeMap(("cts", "2026-09-28T01:02:03Z"), ("src", "did:plc:ewvi7nxzyoun6zhxsrcy6jgr"), ("uri", "at://x"), ("val", "spam"));
        byte[] signed = EncodeMap(
            ("cts", "2026-09-28T01:02:03Z"),
            ("sig", new byte[] { 0xFF, 0xFE }),
            ("src", "did:plc:ewvi7nxzyoun6zhxsrcy6jgr"),
            ("uri", "at://x"),
            ("val", "spam"));

        Assert.Equal(unsigned, LabelEventDecoder.GetUnsignedLabel(signed));
    }

    [Fact]
    public void DecodeLabelReadsTheCborEncoding()
    {
        CborFields fields = FirehoseCbor.ReadFields(EncodeMap(
            ("cts", "2026-09-28T01:02:03Z"),
            ("neg", true),
            ("sig", new byte[] { 1, 2 }),
            ("src", "did:plc:ewvi7nxzyoun6zhxsrcy6jgr"),
            ("uri", "at://did:plc:ewvi7nxzyoun6zhxsrcy6jgr"),
            ("val", "!hide")), LabelEventDecoder.LabelFields);

        Labels.Label label = LabelEventDecoder.DecodeLabel(fields);

        Assert.Null(label.Version);
        Assert.Null(label.Cid);
        Assert.Null(label.ExpiresAt);
        Assert.True(label.IsNegationLabel);
        Assert.Equal([1, 2], label.Signature!);
    }

    [Fact]
    public void DecodeLabelRejectsAnInvalidCid() =>
        Assert.Throws<InvalidDataException>(() => LabelEventDecoder.DecodeLabel(FirehoseCbor.ReadFields(EncodeMap(
            ("cid", "not-a-cid"),
            ("cts", "2026-09-28T01:02:03Z"),
            ("src", "did:plc:ewvi7nxzyoun6zhxsrcy6jgr"),
            ("uri", "at://x"),
            ("val", "spam")), LabelEventDecoder.LabelFields)));

    [Theory]
    [InlineData("takendown", RepoStatus.Takendown)]
    [InlineData("desynchronized", RepoStatus.Desynchronized)]
    [InlineData("throttled", RepoStatus.Throttled)]
    [InlineData("somethingElse", RepoStatus.Unknown)]
    [InlineData(null, RepoStatus.Unknown)]
    public void RepoStatusParseTreatsTheSetAsOpen(string? value, RepoStatus expected) => Assert.Equal(expected, RepoStatusConverter.Parse(value));

    [Fact]
    public void CappedCarReaderRejectsOversizedBlocks()
    {
        byte[] data = new byte[100];
        Cid cid = Cid.FromDagCbor(data);
        using MemoryStream stream = new();
        WriteSection(stream, Encode(writer =>
        {
            writer.WriteStartMap(2);
            writer.WriteTextString("roots");
            writer.WriteStartArray(0);
            writer.WriteEndArray();
            writer.WriteTextString("version");
            writer.WriteInt64(1);
            writer.WriteEndMap();
        }));
        WriteSection(stream, [.. cid.ToBytes(), .. data]);
        stream.Position = 0;

        using CarReader reader = new(stream, maximumBlockSize: 64);
        reader.ReadHeader();

        Assert.Throws<InvalidDataException>(() => reader.ReadBlock());
    }

    [Fact]
    public void CarReaderDoesNotSizeTheRootsFromTheDeclaredCount()
    {
        const int declaredRoots = 500_000;
        byte[] header = Encode(writer =>
        {
            writer.WriteStartMap(2);
            writer.WriteTextString("roots");
            writer.WriteStartArray(declaredRoots);
            for (int i = 0; i < declaredRoots; i++)
            {
                writer.WriteInt32(0);
            }

            writer.WriteEndArray();
            writer.WriteTextString("version");
            writer.WriteInt64(1);
            writer.WriteEndMap();
        });
        using MemoryStream stream = new();
        WriteSection(stream, header);
        stream.Position = 0;
        using CarReader reader = new(stream);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidDataException>(() => reader.ReadHeader());
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Sizing the list from the declared count would allocate a further 4 MB on top of the half megabyte header.
        Assert.True(allocated < 2 * 1024 * 1024, $"Reading the header allocated {allocated} bytes.");
    }

    [Fact]
    public void ReadFieldsSkipsUnknownFieldsWithoutAllocatingThem()
    {
        const int unknownFields = 200_000;
        byte[] map = Encode(writer =>
        {
            writer.WriteStartMap(unknownFields + 1);
            writer.WriteTextString("seq");
            writer.WriteInt64(7);
            for (int i = 0; i < unknownFields; i++)
            {
                writer.WriteTextString(string.Create(CultureInfo.InvariantCulture, $"u{i}"));
                writer.WriteInt32(0);
            }

            writer.WriteEndMap();
        });

        long before = GC.GetAllocatedBytesForCurrentThread();
        CborFields fields = FirehoseCbor.ReadFields(map, new CborFieldNames("seq"));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(7, fields.GetInteger("seq"));
        Assert.False(fields.Contains("u0"));

        // Keeping every field would allocate megabytes of names and dictionary entries for this map of about 1.4 MB.
        Assert.True(allocated < 64 * 1024, $"Reading the map allocated {allocated} bytes.");
    }

    [Fact]
    public void GetUnsignedLabelRejectsLabelsWithTooManyFields()
    {
        (string Key, object Value)[] fields = [.. Enumerable.Range(0, LabelEventDecoder.MaximumSignedLabelFields + 1)
            .Select(i => (string.Create(CultureInfo.InvariantCulture, $"f{i:D3}"), (object)"x"))];

        Assert.Throws<InvalidDataException>(() => LabelEventDecoder.GetUnsignedLabel(EncodeMap(fields)));
        Assert.NotEmpty(LabelEventDecoder.GetUnsignedLabel(EncodeMap(fields[..LabelEventDecoder.MaximumSignedLabelFields])));
    }

    public static TheoryData<Func<FirehoseOptions>> InvalidOptions => new()
    {
        () => new FirehoseOptions { BufferSize = 0 },
        () => new FirehoseOptions { MaxMessageSize = -1 },
        () => new FirehoseOptions { MaximumLabelsPerMessage = 0 },
        () => new FirehoseOptions { MaximumLabelSourcesPerMessage = 0 },
        () => new FirehoseOptions { MaximumCarBlocks = 0 },
        () => new FirehoseOptions { MaximumCarBlockSize = 0 },
        () => new FirehoseOptions { IdleTimeout = TimeSpan.Zero },
        () => new FirehoseOptions { CloseTimeout = TimeSpan.FromDays(100) },
        () => new FirehoseOptions { SigningKeyCacheSize = 0 },
        () => new FirehoseOptions { SigningKeyCacheDuration = TimeSpan.Zero },
        () => new FirehoseOptions { SigningKeyCacheDuration = TimeSpan.FromDays(100) },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions), DisableDiscoveryEnumeration = true)]
    public void InvalidOptionsAreRejected(Func<FirehoseOptions> create) => Assert.Throws<ArgumentOutOfRangeException>(create);

    [Theory]
    [InlineData("ftp://relay.example")]
    [InlineData("file:///relay")]
    [InlineData("wss://user:password@relay.example")]
    public void InvalidHostsAreRejected(string host)
    {
        Assert.Throws<ArgumentException>(() => new FirehoseOptions { RelayUri = new Uri(host) });
        Assert.Throws<ArgumentException>(() => new FirehoseOptions { LabelerUri = new Uri(host) });
    }

    [Fact]
    public void OptionsHaveTheDocumentedDefaults()
    {
        FirehoseOptions options = new();

        Assert.Equal(new Uri("wss://bsky.network"), options.RelayUri);
        Assert.Equal(new Uri("wss://mod.bsky.app"), options.LabelerUri);
        Assert.Equal(5 * 1024 * 1024, options.MaxMessageSize);
        Assert.Equal(TimeSpan.FromMinutes(5), options.IdleTimeout);
        Assert.Equal(10, options.MaximumLabelSourcesPerMessage);
        Assert.False(options.VerifySignatures);
        Assert.True(options.CacheSigningKeys);
        Assert.Equal(100_000, options.SigningKeyCacheSize);
        Assert.Equal(TimeSpan.FromHours(1), options.SigningKeyCacheDuration);
    }

    private static byte[] Encode(Action<CborWriter> write)
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        write(writer);
        return writer.Encode();
    }

    private static byte[] EncodeMap(params (string Key, object Value)[] fields) => Encode(writer =>
    {
        writer.WriteStartMap(fields.Length);

        foreach ((string key, object value) in fields)
        {
            writer.WriteTextString(key);

            switch (value)
            {
                case long integer:
                    writer.WriteInt64(integer);
                    break;
                case string text:
                    writer.WriteTextString(text);
                    break;
                case bool boolean:
                    writer.WriteBoolean(boolean);
                    break;
                case byte[] bytes:
                    writer.WriteByteString(bytes);
                    break;
                default:
                    throw new ArgumentException("Unsupported value.", nameof(fields));
            }
        }

        writer.WriteEndMap();
    });

    // Each level is a one item array (0x81), or a one entry map keyed by the empty string (0xA1 0x60).
    private static byte[] DeeplyNested(byte container, int length)
    {
        int step = container == 0xA1 ? 2 : 1;
        byte[] nested = new byte[length - (length % step) + 1];

        for (int i = 0; i + step <= nested.Length - 1; i += step)
        {
            nested[i] = container;

            if (step == 2)
            {
                nested[i + 1] = 0x60;
            }
        }

        nested[^1] = 0x01;
        return nested;
    }
    private static byte[] Nested(int depth)
    {
        byte[] nested = new byte[depth + 1];
        Array.Fill(nested, (byte)0x81, 0, depth);
        nested[depth] = 0x01;
        return [0xA1, 0x61, 0x61, .. nested];
    }

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
}
