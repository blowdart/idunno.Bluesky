// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.Bluesky.Authentication;

/// <summary>
/// Provides the permission sets published by Bluesky's app and chat lexicons.
/// </summary>
/// <remarks>
/// <para>Sets with inherited RPC permissions target Bluesky's default app view or chat service.
/// Construct an <see cref="OAuthPermissionSet"/> with the same NSID and a different audience to target another service.</para>
/// <para>Request <c>atproto</c> and any required blob, account, or identity scopes separately.</para>
/// <para>See <see href="https://github.com/bluesky-social/atproto/tree/main/lexicons/app/bsky">Bluesky lexicons</see>
/// and <see href="https://github.com/bluesky-social/atproto/tree/main/lexicons/chat/bsky">chat lexicons</see>.</para>
/// </remarks>
public static class BlueskyOAuthPermissionSets
{
    private const string AppViewAudience = "did:web:api.bsky.app#bsky_appview";
    private const string ChatAudience = "did:web:api.bsky.chat#bsky_chat";

    /// <summary>
    /// Gets the permission set for creating posts, without updating or deleting them.
    /// </summary>
    public static OAuthPermissionSet CreatePosts { get; } = new("app.bsky.authCreatePosts", AppViewAudience);

    /// <summary>
    /// Gets the permission set for deleting posts, reposts, and likes.
    /// </summary>
    public static OAuthPermissionSet DeleteContent { get; } = new("app.bsky.authDeleteContent");

    /// <summary>
    /// Gets the permission set for all Bluesky social app features, excluding chat and blob uploads.
    /// </summary>
    public static OAuthPermissionSet FullApp { get; } = new("app.bsky.authFullApp", AppViewAudience);

    /// <summary>
    /// Gets the permission set for managing feed generator declaration records.
    /// </summary>
    public static OAuthPermissionSet ManageFeedDeclarations { get; } = new("app.bsky.authManageFeedDeclarations");

    /// <summary>
    /// Gets the permission set for managing labeler declaration records.
    /// </summary>
    public static OAuthPermissionSet ManageLabelerService { get; } = new("app.bsky.authManageLabelerService");

    /// <summary>
    /// Gets the permission set for managing personal blocks, mutes, moderation lists, services, and preferences.
    /// </summary>
    public static OAuthPermissionSet ManageModeration { get; } = new("app.bsky.authManageModeration", AppViewAudience);

    /// <summary>
    /// Gets the permission set for viewing and configuring notifications.
    /// </summary>
    public static OAuthPermissionSet ManageNotifications { get; } = new("app.bsky.authManageNotifications", AppViewAudience);

    /// <summary>
    /// Gets the permission set for updating profile data, status, and public chat visibility.
    /// </summary>
    public static OAuthPermissionSet ManageProfile { get; } = new("app.bsky.authManageProfile");

    /// <summary>
    /// Gets the permission set for read-only access to content, notifications, and preferences.
    /// </summary>
    public static OAuthPermissionSet ViewAll { get; } = new("app.bsky.authViewAll", AppViewAudience);

    /// <summary>
    /// Gets the permission set for managing all chat conversations and configuration.
    /// </summary>
    public static OAuthPermissionSet FullChatClient { get; } = new("chat.bsky.authFullChatClient", ChatAudience);
}
