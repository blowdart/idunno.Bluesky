// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.BotSdk;

/// <summary>Executes and reconciles one versioned kind of durable action.</summary>
/// <remarks>
/// <para>Register custom handlers as singleton services through DI. Handler names and versions are persisted;
/// retain support for queued versions during upgrades. Use injected clients for custom external services.</para>
/// <para>Return a result only for confirmed success or terminal rejection. Throw on transport failures and
/// unknown outcomes. Reconciliation must prove absence before returning <see langword="null"/> to permit execution.</para>
/// </remarks>
public interface IBotActionHandler
{
    /// <summary>Gets the stable, case-sensitive action kind.</summary>
    string Kind { get; }

    /// <summary>Gets the supported persisted payload version.</summary>
    int PayloadVersion { get; }

    /// <summary>Validates complete saved intent without performing remote operations.</summary>
    /// <param name="action">The action to validate.</param>
    void Validate(BotAction action);

    /// <summary>Reconciles existing remote effects before execution or retry.</summary>
    /// <param name="action">The unchanged durable intent.</param>
    /// <param name="remote">The authenticated Bluesky transport.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmed outcome, or <see langword="null"/> when execution is safe.</returns>
    Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken);

    /// <summary>Executes a previously validated action after its attempt has been durably marked uncertain.</summary>
    /// <param name="action">The unchanged durable intent.</param>
    /// <param name="remote">The authenticated Bluesky transport.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The confirmed outcome; unknown outcomes must throw rather than return success.</returns>
    Task<BotActionResult> ExecuteAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken);
}

/// <summary>Describes a confirmed remote effect or terminal rejection.</summary>
/// <param name="Succeeded"><see langword="true"/> for a confirmed effect; otherwise, <see langword="false"/> for rejection.</param>
/// <param name="RemoteReference">The remote effect's URI, including a non-AT URI for application-defined actions, or <see langword="null"/> when rejected.</param>
/// <param name="Reason">The diagnostic reason, without credentials or other secrets.</param>
public sealed record BotActionResult(
    bool Succeeded,
    string? RemoteReference,
    string Reason);

/// <summary>Provides authenticated operations used by the built-in action handlers.</summary>
/// <remarks>
/// <para>Methods throw on unsuccessful remote requests. Only an explicit repository RecordNotFound may
/// become a missing record. Collection scans must exhaust pagination before reporting absence.</para>
/// <para>The runtime supplies this transport. Custom handlers can also use their own DI-injected clients.</para>
/// </remarks>
public interface IBotActionRemote
{
    /// <summary>Gets the authenticated SDK agent for custom Bluesky operations using the same protected, accounted transport.</summary>
    /// <remarks><para>The runtime owns this agent. Handlers must not dispose it, replace its credentials,
    /// or use a separate transport to bypass durable rate accounting.</para></remarks>
    BlueskyAgent Agent { get; }

    /// <summary>Gets the authenticated bot's DID.</summary>
    string BotDid { get; }

    /// <summary>Reads a built-in action's fixed repository URI, preserving the returned record as JSON.</summary>
    /// <param name="action">The action identifying the record kind and fixed key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The record JSON, or <see langword="null"/> only for explicit RecordNotFound.</returns>
    Task<string?> FindRecordAsync(
        BotAction action,
        CancellationToken cancellationToken);

    /// <summary>Creates the complete saved built-in repository record at its fixed key.</summary>
    /// <param name="action">The complete action to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The confirmed create result or terminal rejection.</returns>
    Task<BotActionResult> CreateRecordAsync(
        BotAction action,
        CancellationToken cancellationToken);

    /// <summary>Reads the source post and current viewer state without treating AppView absence as definitive deletion.</summary>
    /// <param name="uri">The post URI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The post view.</returns>
    Task<PostView> TargetAsync(
        AtUri uri,
        CancellationToken cancellationToken);

    /// <summary>Checks bookmark pages for a post, including blocked or unavailable bookmarked posts.</summary>
    /// <param name="uri">The bookmarked post URI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the post is bookmarked; otherwise, <see langword="false"/> after exhaustive scanning.</returns>
    Task<bool> HasBookmarkAsync(
        AtUri uri,
        CancellationToken cancellationToken);

    /// <summary>Creates a service-managed bookmark using the original saved strong reference.</summary>
    /// <param name="subject">The post URI and CID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CreateBookmarkAsync(
        StrongReference subject,
        CancellationToken cancellationToken);

    /// <summary>Checks an owned curation list for an existing author membership.</summary>
    /// <param name="listUri">The bot-owned curation list URI.</param>
    /// <param name="author">The author's DID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The existing list-item URI, or <see langword="null"/> after exhaustive scanning.</returns>
    Task<string?> FindListMemberAsync(
        AtUri listUri,
        Did author,
        CancellationToken cancellationToken);
}
