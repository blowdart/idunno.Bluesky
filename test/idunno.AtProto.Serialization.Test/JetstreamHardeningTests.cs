// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Models;
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
    [Theory]
    [InlineData("collection")]
    [InlineData("rkey")]
    [InlineData("rev")]
    public void AnExplicitNullForAGuardedCommitPropertyIsRejected(string property)
    {
        string json = $$"""
            {
              "did": "{{TestDid}}",
              "time_us": 1725911162329308,
              "kind": "commit",
              "commit": {
                "rev": "3l3qo2vutsw2b",
                "operation": "create",
                "collection": "app.bsky.feed.like",
                "rkey": "3l3qo2vuowo2b"
              }
            }
            """.Replace($"\"{property}\": \"3l3qo2vutsw2b\"", $"\"{property}\": null", StringComparison.Ordinal)
               .Replace($"\"{property}\": \"app.bsky.feed.like\"", $"\"{property}\": null", StringComparison.Ordinal)
               .Replace($"\"{property}\": \"3l3qo2vuowo2b\"", $"\"{property}\": null", StringComparison.Ordinal);

        // Marking a property required only makes the serializer insist it is present, so an explicit null would
        // otherwise be written straight into a non-nullable property.
        Assert.ThrowsAny<ArgumentNullException>(
            () => JsonSerializer.Deserialize<AtJetstreamCommitEvent>(json, SourceGenerationContext.Default.AtJetstreamCommitEvent));
    }

    [Fact]
    public void AnExplicitNullForTheEventDidIsRejected()
    {
        string json = """
            {"did":null,"time_us":1725911162329308,"kind":"identity"}
            """;

        Assert.ThrowsAny<ArgumentNullException>(
            () => JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent));
    }

    [Theory]
    [InlineData("create", JetstreamCommitOperation.Create)]
    [InlineData("update", JetstreamCommitOperation.Update)]
    [InlineData("delete", JetstreamCommitOperation.Delete)]
    [InlineData("truncate", JetstreamCommitOperation.Unknown)]
    [InlineData("Create", JetstreamCommitOperation.Unknown)]
    public void ACommitOperationDeserializesToItsValueOrToUnknown(string operation, JetstreamCommitOperation expected)
    {
        string json = $$"""
            {
              "did": "{{TestDid}}",
              "time_us": 1725911162329308,
              "kind": "commit",
              "commit": {
                "rev": "3l3qo2vutsw2b",
                "operation": "{{operation}}",
                "collection": "app.bsky.feed.like",
                "rkey": "3l3qo2vuowo2b"
              }
            }
            """;

        AtJetstreamCommitEvent? actual = JsonSerializer.Deserialize<AtJetstreamCommitEvent>(
            json,
            SourceGenerationContext.Default.AtJetstreamCommitEvent);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual.Commit.Operation);
    }

    [Theory]
    [InlineData(JetstreamCommitOperation.Create, "create")]
    [InlineData(JetstreamCommitOperation.Update, "update")]
    [InlineData(JetstreamCommitOperation.Delete, "delete")]
    public void ACommitOperationSerializesToTheValueTheJetstreamUses(JetstreamCommitOperation operation, string expected)
    {
        string json = JsonSerializer.Serialize(operation, SourceGenerationContext.Default.JetstreamCommitOperation);

        Assert.Equal($"\"{expected}\"", json);
    }

    [Fact]
    public void AnOptionsUpdateUsesThePropertyNameTheJetstreamReadsForTheDidFilter()
    {
        OptionsUpdateMessage message = new()
        {
            Payload = new OptionsUpdatePayload
            {
                MaxMessageSizeBytes = 0,
                WantedDIDs = [new Did(TestDid)]
            }
        };

        string json = JsonSerializer.Serialize(message, SourceGenerationContext.Default.OptionsUpdateMessage);

        // The camel case naming policy produces "wantedDIDs" from the property name, which a server matching names
        // exactly would read as no did filter at all, which is a subscription to everything.
        Assert.Contains("\"wantedDids\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"wantedDIDs\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAccountDoesNotSerializeATypeDiscriminatorTheJetstreamDoesNotUse()
    {
        AtJetstreamAccountEvent accountEvent = new()
        {
            Did = new Did(TestDid),
            TimeStamp = 1725911162329308,
            Kind = JetStreamEventKind.Account,
            Account = new AtJetstreamAccount
            {
                Active = true,
                Did = new Did(TestDid),
                Sequence = 1
            }
        };

        string json = JsonSerializer.Serialize(accountEvent, SourceGenerationContext.Default.AtJetstreamAccountEvent);

        Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIdentityDoesNotSerializeATypeDiscriminatorTheJetstreamDoesNotUse()
    {
        AtJetstreamIdentityEvent identityEvent = new()
        {
            Did = new Did(TestDid),
            TimeStamp = 1725911162329308,
            Kind = JetStreamEventKind.Identity,
            Identity = new AtJetStreamIdentity
            {
                Did = new Did(TestDid),
                Sequence = 1
            }
        };

        string json = JsonSerializer.Serialize(identityEvent, SourceGenerationContext.Default.AtJetstreamIdentityEvent);

        Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DerivingAnEventKeepsExtensionDataOtherThanTheKeyItConsumed()
    {
        string json = $$"""
            {
              "did": "{{TestDid}}",
              "time_us": 1725911162329308,
              "kind": "commit",
              "unknownFromAFutureServer": "keep me",
              "commit": {
                "rev": "3l3qo2vutsw2b",
                "operation": "create",
                "collection": "app.bsky.feed.like",
                "rkey": "3l3qo2vuowo2b"
              }
            }
            """;

        AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent);
        Assert.NotNull(actual);

        using var jetstream = new AtProtoJetstream();

        AtJetstreamCommitEvent derived = Assert.IsType<AtJetstreamCommitEvent>(jetstream.DeriveEvent(actual));

        Assert.NotNull(derived.ExtensionData);
        Assert.False(derived.ExtensionData.ContainsKey("commit"));
        Assert.True(derived.ExtensionData.TryGetValue("unknownFromAFutureServer", out JsonElement unknown));
        Assert.Equal("keep me", unknown.GetString());
    }
}
