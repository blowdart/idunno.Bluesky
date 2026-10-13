// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Jetstream;

using Samples.BotSdk;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks sample eligibility and bounded replay generation recovery.</summary>
public sealed class ReplayEligibilityTests
{
    /// <summary>Rejects consecutive and nonconsecutive repeated cursors without rejecting distinct pages.</summary>
    /// <param name="nonconsecutive">Whether a different cursor intervenes before repetition.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArchiveScanRejectsRepeatedCursors(bool nonconsecutive)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        ReaderWorker.ValidateArchiveCursor(seen, "A");
        if (nonconsecutive)
        {
            ReaderWorker.ValidateArchiveCursor(seen, "B");
            ReaderWorker.ValidateArchiveCursor(seen, "a");
        }

        Assert.Throws<InvalidDataException>(() => ReaderWorker.ValidateArchiveCursor(seen, "A"));
        ReaderWorker.ValidateArchiveCursor(seen, null);
        Assert.Equal(nonconsecutive ? 3 : 1, seen.Count);
    }

    /// <summary>Audits only JSON parsing failures and propagates policy errors before progress can commit.</summary>
    /// <param name="failureKind">The policy failure category.</param>
    [Theory]
    [InlineData("json")]
    [InlineData("kind")]
    [InlineData("key")]
    [InlineData("argument")]
    [InlineData("data")]
    public void PolicyValidationErrorsAreNotMalformedSourceData(string failureKind)
    {
        FailingPolicy policy = new(failureKind);
        if (failureKind == "json")
        {
            Assert.Empty(ReaderWorker.EvaluatePolicy(policy, TestRecords.Event(), TestRecords.Start, out JsonException? malformed));
            Assert.Same(policy.Failure, malformed);
        }
        else
        {
            Exception failure = Assert.ThrowsAny<Exception>(() =>
                ReaderWorker.EvaluatePolicy(policy, TestRecords.Event(), TestRecords.Start, out _));
            Assert.True(failure is ArgumentException or InvalidDataException);
        }
    }

    private sealed class FailingPolicy(string failureKind) : IBotPolicy
    {
        public Exception Failure { get; } = failureKind switch
        {
            "json" => new JsonException("Malformed source record."),
            "data" => new InvalidDataException("Policy data failure."),
            _ => new ArgumentException("Policy configuration failure.")
        };

        public IReadOnlyList<BotAction> Evaluate(JetstreamEvent item, DateTimeOffset startedAt)
        {
            if (failureKind is "kind" or "key")
            {
                BotAction reply = TestRecords.Work();
                return [BotActions.Create(
                    failureKind == "kind" ? "" : "custom",
                    new(reply.SourceUri, new(TestRecords.CidValue)),
                    11,
                    new CustomNotificationPayload("saved"),
                    ActionHandlerJsonContext.Default.CustomNotificationPayload,
                    actionKey: failureKind == "key" ? "" : "default")];
            }

            throw Failure;
        }
    }

    /// <summary>Uses the durable witness boundary, even when the post was authored earlier.</summary>
    /// <param name="seconds">The witnessed-time offset, or a missing timestamp.</param>
    /// <param name="eligible">Whether a reply should be selected.</param>
    [Theory]
    [InlineData(null, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void SampleUsesWitnessedTimeRatherThanPostCreationTime(int? seconds, bool eligible)
    {
        var item = TestRecords.Event("#beans") with
        {
            WitnessedAt = seconds is int offset ? TestRecords.Start.AddSeconds(offset) : null
        };
        BeansBotPolicy policy = new();
        Assert.Equal(eligible ? 1 : 0, policy.Evaluate(item, TestRecords.Start).Count);
    }

    /// <summary>Resets independent recovery episodes without allowing repeated prefixes to evade the bound.</summary>
    [Fact]
    public void GenerationRecoveryResetsOnlyBeyondPreviouslyHandledProgress()
    {
        GenerationRecovery recovery = new();
        recovery.Observe(10);
        for (int incident = 0; incident < 10; incident++)
        {
            for (int retry = 1; retry <= 5; retry++)
            {
                recovery.Retry();
                recovery.Observe(10 + incident);
                recovery.Observe(9);
                Assert.Equal(retry, recovery.Retries);
            }

            recovery.Observe(11 + incident);
            Assert.Equal(0, recovery.Retries);
        }
    }
}
