// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

using idunno.AtProto.Jetstream;
namespace idunno.AtProto.Serialization.Test;

[ExcludeFromCodeCoverage]
public class JetstreamHardeningTests
{
    private const string TestDid = "did:plc:eygmaihciaxprqvxpfvl6flk";

    private static string EventWithKind(string kind) => $$"""
        {"did":"{{TestDid}}","time_us":1725911162329308,"kind":"{{kind}}"}
        """;

    private static string EventWithTimestamp(long timeStamp) => $$"""
        {"did":"{{TestDid}}","time_us":{{timeStamp}},"kind":"identity"}
        """;

    [Theory]
    [InlineData("sync")]
    [InlineData("labels")]
    [InlineData("")]
    [InlineData("Commit")]
    public void AnEventKindWhichIsNotKnownDeserializesAsUnknown(string kind)
    {
        // The set of kinds a jetstream emits is decided by the server, so one added upstream must not make the
        // whole event unreadable.
        AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(
            EventWithKind(kind),
            SourceGenerationContext.Default.AtJetstreamEvent);

        Assert.NotNull(actual);
        Assert.Equal(JetStreamEventKind.Unknown, actual.Kind);
        Assert.Equal(TestDid, actual.Did);
    }

    [Theory]
    [InlineData("account", JetStreamEventKind.Account)]
    [InlineData("commit", JetStreamEventKind.Commit)]
    [InlineData("identity", JetStreamEventKind.Identity)]
    public void AKnownEventKindStillDeserializesToItsValue(string kind, JetStreamEventKind expected)
    {
        AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(
            EventWithKind(kind),
            SourceGenerationContext.Default.AtJetstreamEvent);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual.Kind);
    }

    [Fact]
    public void AnEventKindWhichIsNotAStringThrows()
    {
        string json = $$"""
            {"did":"{{TestDid}}","time_us":1725911162329308,"kind":7}
            """;

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent));
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(-9000000000000000000)]
    [InlineData(9000000000000000000)]
    public void ATimestampOutsideTheRangeOfADateTimeOffsetIsClamped(long timeStamp)
    {
        AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(
            EventWithTimestamp(timeStamp),
            SourceGenerationContext.Default.AtJetstreamEvent);

        Assert.NotNull(actual);

        // A property getter reading remote input must not throw.
        DateTimeOffset dateTimeOffset = actual.DateTimeOffset;

        Assert.Equal(timeStamp < 0 ? DateTimeOffset.MinValue : DateTimeOffset.MaxValue, dateTimeOffset);
    }

    [Fact]
    public void ATimestampInsideTheRangeOfADateTimeOffsetIsNotClamped()
    {
        AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(
            EventWithTimestamp(1725911162329308),
            SourceGenerationContext.Default.AtJetstreamEvent);

        Assert.NotNull(actual);
        Assert.Equal(DateTimeOffset.Parse("2024-09-09T19:46:02.329Z", CultureInfo.InvariantCulture).ToUniversalTime(), actual.DateTimeOffset);
    }

    [Fact]
    public void CopyingAnEventWithANewTimestampReportsTheNewTimestamp()
    {
        AtJetstreamEvent original = new()
        {
            Did = new Did(TestDid),
            TimeStamp = 1725911162329308,
            Kind = JetStreamEventKind.Identity
        };

        // Reading it first is what used to leave a copy reporting the timestamp of the event it was copied from.
        DateTimeOffset originalDateTimeOffset = original.DateTimeOffset;

        AtJetstreamEvent copy = original with { TimeStamp = 1746663645473657 };

        Assert.NotEqual(originalDateTimeOffset, copy.DateTimeOffset);
        Assert.Equal(2025, copy.DateTimeOffset.Year);
        Assert.Equal(2024, originalDateTimeOffset.Year);
    }

    [Fact]
    public void ACommitRecordIsReadableAsJson()
    {
        string json = """
            {
              "did": "did:plc:eygmaihciaxprqvxpfvl6flk",
              "time_us": 1725911162329308,
              "kind": "commit",
              "commit": {
                "rev": "3l3qo2vutsw2b",
                "operation": "create",
                "collection": "app.bsky.feed.like",
                "rkey": "3l3qo2vuowo2b",
                "record": {
                  "$type": "app.bsky.feed.like",
                  "createdAt": "2024-09-09T19:46:02.102Z"
                }
              }
            }
            """;

        AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent);
        Assert.NotNull(actual);

        using var jetstream = new AtProtoJetstream();

        AtJetstreamCommitEvent derived = Assert.IsType<AtJetstreamCommitEvent>(jetstream.DeriveEvent(actual));

        Assert.NotNull(derived.Commit.Record);
        Assert.Equal("app.bsky.feed.like", derived.Commit.Record.Value.GetProperty("$type").GetString());
    }
}
