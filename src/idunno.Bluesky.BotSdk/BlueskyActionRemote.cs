// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Bookmarks;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.BotSdk;

/// <summary>Translates built-in action operations into SDK calls using protected transport and generated JSON metadata.</summary>
/// <param name="session">The authenticated long-lived SDK session.</param>
/// <param name="options">The expected bot account identity.</param>
/// <param name="httpClientFactory">The same protected and durably accounted client factory used by the agent.</param>
internal sealed class BlueskyActionRemote(
    BlueskySession session,
    BotOptions options,
    IHttpClientFactory httpClientFactory) : IBotActionRemote
{
    /// <inheritdoc/>
    public BlueskyAgent Agent => session.Agent;

    /// <inheritdoc/>
    public string BotDid => options.Did;

    /// <inheritdoc/>
    public Task<string?> FindRecordAsync(
        BotAction action,
        CancellationToken cancellationToken) =>
        action.ActionKind switch
        {
            "reply" => FindAsync(action, BotJson.Post, cancellationToken),
            "like" => FindAsync(action, BotJson.Like, cancellationToken),
            "repost" => FindAsync(action, BotJson.Repost, cancellationToken),
            "list-item" => FindListItemAsync(action, cancellationToken),
            _ => throw new InvalidOperationException($"No built-in record transport for '{action.ActionKind}'.")
        };

    /// <summary>Reads a fixed repository URI and treats only explicit RecordNotFound as safe absence.</summary>
    /// <typeparam name="T">The SDK record type with generated serialization metadata.</typeparam>
    /// <param name="action">The kind and fixed key identifying the saved remote record.</param>
    /// <param name="metadata">The SDK's generated metadata preserving all returned record fields.</param>
    /// <param name="cancellationToken">The active ownership/shutdown token.</param>
    /// <returns>The complete returned record JSON, or <see langword="null"/> for explicit absence.</returns>
    private async Task<string?> FindAsync<T>(
        BotAction action,
        JsonTypeInfo<T> metadata,
        CancellationToken cancellationToken)
        where T : BlueskyRecord
    {
        AtUri uri = ActionRecords.Uri(action, this);
        AtProtoHttpResult<AtProtoRepositoryRecord<T>> found =
            await session.Agent.GetBlueskyRecord<T>(uri, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (found.Succeeded)
        {
            if (found.Result.Uri != uri)
            {
                throw new InvalidDataException("The PDS returned an unexpected fixed record URI.");
            }

            return JsonSerializer.Serialize(found.Result.Value, metadata);
        }
        if (found.AtErrorDetail?.Error == "RecordNotFound" &&
            found.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            return null;
        }

        throw Failure(found, "reconciling a fixed record");
    }

    /// <summary>Reads raw list-item JSON through the SDK's generated non-generic response contract.</summary>
    /// <param name="action">The saved membership key whose URI belongs to the authenticated bot.</param>
    /// <param name="cancellationToken">The active ownership/shutdown token.</param>
    /// <returns>The exact returned payload, or <see langword="null"/> only for explicit RecordNotFound.</returns>
    private async Task<string?> FindListItemAsync(
        BotAction action,
        CancellationToken cancellationToken)
    {
        using HttpClient client = httpClientFactory.CreateClient("idunno.atproto");
        AtProtoHttpResult<AtProtoRepositoryRecord> found = await AtProtoServer.GetRecord(
            new Did(BotDid),
            new Nsid("app.bsky.graph.listitem"),
            new RecordKey(action.RecordKey),
            cid: null,
            service: Agent.Service,
            accessCredentials: null,
            httpClient: client,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (found.Succeeded)
        {
            if (found.Result.Uri != ActionRecords.Uri(action, this))
            {
                throw new InvalidDataException("The PDS returned an unexpected fixed list-item URI.");
            }

            return found.Result.Value?.ToJsonString()
                ?? throw new InvalidDataException("The PDS returned a null list-item payload.");
        }
        if (found.AtErrorDetail?.Error == "RecordNotFound" &&
            found.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            return null;
        }

        throw Failure(found, "reconciling a fixed list item");
    }

    /// <inheritdoc/>
    public Task<BotActionResult> CreateRecordAsync(
        BotAction action,
        CancellationToken cancellationToken) =>
        action.ActionKind switch
        {
            "reply" => CreateAsync(action, BotJson.Post, cancellationToken),
            "like" => CreateAsync(action, BotJson.Like, cancellationToken),
            "repost" => CreateAsync(action, BotJson.Repost, cancellationToken),
            "list-item" => CreateListItemAsync(action, cancellationToken),
            _ => throw new InvalidOperationException($"No built-in record transport for '{action.ActionKind}'.")
        };

    /// <summary>Creates the saved repository payload at its fixed key and validates the server's returned URI.</summary>
    /// <typeparam name="T">The SDK record type with generated serialization metadata.</typeparam>
    /// <param name="action">The unchanged saved key and complete payload.</param>
    /// <param name="metadata">The generated payload metadata.</param>
    /// <param name="cancellationToken">The active ownership/shutdown token.</param>
    /// <returns>The confirmed creation or a known terminal target rejection.</returns>
    private async Task<BotActionResult> CreateAsync<T>(
        BotAction action,
        JsonTypeInfo<T> metadata,
        CancellationToken cancellationToken)
        where T : BlueskyRecord
    {
        T record = ActionRecords.Read(action, metadata);
        AtProtoHttpResult<CreateRecordResult> created = await session.Agent.CreateBlueskyRecord(
            record,
            new Nsid(ActionRecords.Collection(action.ActionKind)),
            rKey: new RecordKey(action.RecordKey),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (created.Succeeded)
        {
            AtUri expected = ActionRecords.Uri(action, this);
            if (created.Result.Uri != expected)
            {
                throw new InvalidDataException("The PDS returned an unexpected URI for the fixed record key.");
            }

            return new BotActionResult(true, expected.ToString(), "created");
        }
        if (created.AtErrorDetail?.Error is "ReplyNotAllowed" or "InvalidReply" or "BlockedActor" or "BlockedByActor" or "RecordNotFound")
        {
            return new BotActionResult(false, null, $"server rejected target: {created.AtErrorDetail.Error}");
        }

        throw Failure(created, "creating a fixed record");
    }

    /// <summary>Publishes the saved list-item fields through the SDK's generated extension-data record contract.</summary>
    /// <param name="action">The saved list, author, creation time, and stable membership key.</param>
    /// <param name="cancellationToken">The active ownership/shutdown token.</param>
    /// <returns>The confirmed fixed-URI membership creation.</returns>
    private async Task<BotActionResult> CreateListItemAsync(
        BotAction action,
        CancellationToken cancellationToken)
    {
        // The SDK's list-item type is internal. Its public base record has generated
        // extension-data metadata, which preserves this already serialized payload.
        using JsonDocument payload = JsonDocument.Parse(action.RecordJson);
        BlueskyRecord item = new()
        {
            ExtensionData = payload.RootElement.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.Clone())
        };
        AtProtoHttpResult<CreateRecordResult> result = await session.Agent.CreateBlueskyRecord(
            item,
            new Nsid("app.bsky.graph.listitem"),
            rKey: new RecordKey(action.RecordKey),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw Failure(result, "creating a list item");
        }
        AtUri expected = ActionRecords.Uri(action, this);
        if (result.Result.Uri != expected)
        {
            throw new InvalidDataException("The PDS returned an unexpected fixed list-item URI.");
        }

        return new BotActionResult(true, expected.ToString(), "created");
    }

    /// <inheritdoc/>
    public async Task<PostView> TargetAsync(
        AtUri uri,
        CancellationToken cancellationToken)
    {
        AtProtoHttpResult<PostView> result = await session.Agent.GetPostView(uri, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new RemoteFailureException(result.StatusCode, result.AtErrorDetail?.Error, "checking an action target", retryMissing: true);
        }

        return result.Result;
    }

    /// <inheritdoc/>
    public async Task<bool> HasBookmarkAsync(
        AtUri uri,
        CancellationToken cancellationToken)
    {
        string? cursor = null;
        HashSet<string> cursors = new(StringComparer.Ordinal);
        do
        {
            AtProtoHttpResult<PagedViewReadOnlyCollection<BookmarkView>> result = await session.Agent.GetBookmarks(
                limit: 100,
                cursor: cursor,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw Failure(result, "reconciling bookmarks");
            }
            if (result.Result.Any(bookmark => bookmark.Subject.Uri == uri))
            {
                return true;
            }
            cursor = result.Result.Cursor;
            CheckCursor(cursor, cursors);
        } while (!string.IsNullOrEmpty(cursor));

        return false;
    }

    /// <inheritdoc/>
    public async Task CreateBookmarkAsync(
        StrongReference subject,
        CancellationToken cancellationToken)
    {
        AtProtoHttpResult<EmptyResponse> result = await session.Agent.CreateBookmark(subject, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw Failure(result, "creating a bookmark");
        }
    }

    /// <inheritdoc/>
    public async Task<string?> FindListMemberAsync(
        AtUri listUri,
        Did author,
        CancellationToken cancellationToken)
    {
        if (listUri.ToString().Split('/')[2] != BotDid)
        {
            throw new InvalidOperationException("Author-list actions require a list owned by the bot account.");
        }
        string? cursor = null;
        HashSet<string> cursors = new(StringComparer.Ordinal);
        do
        {
            AtProtoHttpResult<ListViewWithItems> result = await session.Agent.GetList(
                listUri,
                limit: 100,
                cursor: cursor,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw Failure(result, "checking author list membership");
            }
            if (result.Result.List.Purpose != ListPurpose.CurateList)
            {
                throw new InvalidOperationException("Author-list actions require a curation list, not a moderation or reference list.");
            }
            ListItemView? existing = result.Result.FirstOrDefault(item => item.Subject.Did == author);
            if (existing is not null)
            {
                return existing.Uri.ToString();
            }
            cursor = result.Result.Cursor;
            CheckCursor(cursor, cursors);
        } while (!string.IsNullOrEmpty(cursor));

        return null;
    }

    /// <summary>Rejects pagination loops so an incomplete scan can never authorize another uncertain write.</summary>
    /// <param name="cursor">The next server cursor, or <see langword="null"/> when the scan is exhausted.</param>
    /// <param name="cursors">The cursors already visited in this scan.</param>
    private static void CheckCursor(
        string? cursor,
        HashSet<string> cursors)
    {
        if (!string.IsNullOrEmpty(cursor) && !cursors.Add(cursor))
        {
            throw new InvalidDataException("Remote pagination repeated a cursor; refusing to infer absence.");
        }
    }

    /// <summary>Retains the observed server status and protocol error for the sender's recovery decision.</summary>
    /// <typeparam name="T">The SDK response payload type.</typeparam>
    /// <param name="result">The unsuccessful SDK result.</param>
    /// <param name="operation">The operation being attempted.</param>
    /// <returns>The classified remote failure.</returns>
    private static RemoteFailureException Failure<T>(
        AtProtoHttpResult<T> result,
        string operation) =>
        new(result.StatusCode, result.AtErrorDetail?.Error, operation);
}
