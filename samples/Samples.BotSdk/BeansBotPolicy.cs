// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.BotSdk;

namespace Samples.BotSdk;

/// <summary>
/// A sample bot policy that listens to the live Jetstream event stream,
/// identifies posts containing the hashtag "#beans", and enqueues a reply with the bean emoji "🫘".
/// </summary>
/// <remarks>
/// <para>
/// Bot policies implement the <see cref="IBotPolicy"/> interface.
/// The policy is purely evaluative: it inspects an incoming <see cref="JetstreamEvent"/>
/// and returns a list of intended <see cref="BotAction"/> items to execute.
/// </para>
/// <para>
/// <strong>Important Architectural Rule:</strong> Policies must never make network calls directly.
/// The durable bot runtime handles all network interactions asynchronously:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <strong>Ingestion:</strong> The reader worker streams events and calls <see cref="Evaluate"/>.
/// When actions are returned, they are saved to database storage alongside the stream progress
/// in a single atomic transaction.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Dispatch:</strong> A separate sender worker picks up queued actions from the database outbox,
/// manages rate limits and token refresh, and posts them to Bluesky. If a network call fails,
/// the action remains safely in the database to be retried.
/// </description>
/// </item>
/// </list>
/// </remarks>
public sealed class BeansBotPolicy : IBotPolicy
{
    // The trigger text the bot watches for in posts (matches "#beans", "#Beans", "#BEANS", etc.).
    private const string TriggerTag = "#beans";

    // The reply text to post when the trigger tag is detected.
    private const string ReplyText = "🫘";

    /// <summary>
    /// Evaluates an incoming Jetstream event to decide if any bot actions should be enqueued.
    /// </summary>
    /// <param name="item">The event received from the Jetstream stream.</param>
    /// <param name="startedAt">The durable start time used to exclude earlier witnessed events.</param>
    /// <returns>
    /// A list of <see cref="BotAction"/> items to be saved and dispatched.
    /// Returns an empty list if no action is needed for this event.
    /// </returns>
    /// <exception cref="JsonException">An eligible post record is malformed or deserializes to <see langword="null"/>.</exception>
    public IReadOnlyList<BotAction> Evaluate(JetstreamEvent item, DateTimeOffset startedAt)
    {
        // Archive replay can start at a segment boundary before the bot's durable start time.
        // Use the server's witnessed time, not the author's post creation time, to reject
        // earlier events. Missing witnessed times cannot establish eligibility.
        if (item.WitnessedAt is not DateTimeOffset witnessedAt || witnessedAt < startedAt)
        {
            return [];
        }

        // ---------------------------------------------------------------------------------
        // Step 1: Filter for new post creation events.
        // ---------------------------------------------------------------------------------
        // The Jetstream firehose delivers many event types: record commits, identity changes,
        // account status updates, etc. We only care about commit events where a new record
        // was created (Operation == Create) in the Bluesky post collection ("app.bsky.feed.post"),
        // and that have a valid CID and sequence number.
        //
        // This if statement uses C# pattern matching to check the event's type and properties
        // together, rather than casting the event and checking each property separately.
        // JetstreamCommitEvent { ... } is a type check combined with a property pattern:
        // - Sequence: { } sequence matches a non-null sequence number and captures its value
        //   in the local variable sequence. The empty property pattern { } means "not null".
        // - Commit: { ... } applies a nested property pattern to the event's commit.
        // - Operation: JetstreamCommitOperation.Create matches that specific enum value.
        // - Record: not null requires a record payload.
        // - Cid: { } cid requires a non-null CID and captures it in the local variable cid.
        // The trailing commitEvent captures the matching event for use below.
        //
        // "is not" makes the condition true when this pattern fails. The || operator
        // short-circuits: it checks the collection only when the pattern has matched, so
        // commitEvent is available. If either check rejects the event, we return immediately;
        // otherwise commitEvent, sequence, and cid are available for the remaining steps.
        // Microsoft explains pattern matching here:
        // https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching
        if (item is not JetstreamCommitEvent
            {
                Sequence: { } sequence,
                Commit:
                {
                    Operation: JetstreamCommitOperation.Create,
                    Record: not null,
                    Cid: { } cid
                }
            } commitEvent ||
            commitEvent.Commit.Collection != "app.bsky.feed.post")
        {
            // Not a new post creation or missing required sequence/CID; return an empty list.
            return [];
        }

        // ---------------------------------------------------------------------------------
        // Step 2: Deserialize the post record.
        // ---------------------------------------------------------------------------------
        // The Jetstream event payload contains the raw JSON of the record in Commit.Record.
        // We deserialize it into a strongly-typed Bluesky Post record using the SDK's
        // source-generated serializer options (BlueskyJsonSerializerOptions.Options)
        // for AOT and trimming safety.
        // Let the reader audit malformed source records before committing progress.
        Post post = commitEvent.Commit.Record.Value.Deserialize<Post>(BlueskyJsonSerializerOptions.Options)
            ?? throw new JsonException("The post record is null.");

        if (string.IsNullOrWhiteSpace(post.Text))
        {
            return [];
        }

        // ---------------------------------------------------------------------------------
        // Step 3: Check for our trigger hashtag.
        // ---------------------------------------------------------------------------------
        // We inspect the post text for "#beans" using a case-insensitive search.
        // In Bluesky, users might type "#beans", "#Beans", or "#BEANS", so StringComparison.OrdinalIgnoreCase
        // ensures we catch all variations without culture-sensitive string mapping issues.
        if (!post.Text.Contains(TriggerTag, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        // ---------------------------------------------------------------------------------
        // Step 4: Build a strong reference to the post we are replying to.
        // ---------------------------------------------------------------------------------
        // A StrongReference uniquely identifies a specific revision of a record:
        // - Uri: The AT Protocol URI: at://<author-did>/app.bsky.feed.post/<record-key>
        // - Cid: The Content Identifier (hash) of the record content.
        StrongReference sourcePost = new(
            new AtUri($"at://{commitEvent.Did}/app.bsky.feed.post/{commitEvent.Commit.RKey}"),
            cid);

        // ---------------------------------------------------------------------------------
        // Step 5: Establish conversation threading references.
        // ---------------------------------------------------------------------------------
        // In Bluesky, replies require thread context so the app can display threads properly:
        // - Root  : The top-level post that started the entire conversation thread.
        // - Parent: The immediate post being replied to directly.
        //
        // If the post we matched is already a reply in an existing thread (post.Reply is not null),
        // we preserve its existing Root reference so our reply stays nested in the same thread.
        // If the post is an original top-level post, it becomes both the Root and Parent.
        ReplyReferences replyReferences = post.Reply is not null
            ? new ReplyReferences(root: post.Reply.Root, parent: sourcePost)
            : new ReplyReferences(root: sourcePost, parent: sourcePost);

        // ---------------------------------------------------------------------------------
        // Step 6: Create the reply post record.
        // ---------------------------------------------------------------------------------
        // We construct a new Post record containing our bean emoji reply text ("🫘"),
        // the current UTC timestamp, and the threading references established above.
        Post reply = new(
            text: ReplyText,
            createdAt: DateTimeOffset.UtcNow,
            reply: replyReferences);

        // ---------------------------------------------------------------------------------
        // Step 7: Create and return the durable reply action.
        // ---------------------------------------------------------------------------------
        // BotActions.Reply() bundles:
        // - The source post reference (for deduplication and tracking).
        // - The Jetstream sequence number (ensures ordering and prevents duplicate processing).
        // - The reply Post payload to publish to Bluesky.
        //
        // The durable runtime will save this action to storage in the same transaction as
        // the stream progress checkpoint. Once committed, the background sender worker will
        // dispatch it to the network safely.
        return [BotActions.Reply(sourcePost, sequence, reply)];
    }
}
