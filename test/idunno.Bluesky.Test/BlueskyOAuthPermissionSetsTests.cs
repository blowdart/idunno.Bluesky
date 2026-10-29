// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;
using idunno.Bluesky.Authentication;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class BlueskyOAuthPermissionSetsTests
{
    public static TheoryData<string, string, string?> PublishedPermissionSets => new()
    {
        { nameof(BlueskyOAuthPermissionSets.CreatePosts), "app.bsky.authCreatePosts", "did:web:api.bsky.app#bsky_appview" },
        { nameof(BlueskyOAuthPermissionSets.DeleteContent), "app.bsky.authDeleteContent", null },
        { nameof(BlueskyOAuthPermissionSets.FullApp), "app.bsky.authFullApp", "did:web:api.bsky.app#bsky_appview" },
        { nameof(BlueskyOAuthPermissionSets.ManageFeedDeclarations), "app.bsky.authManageFeedDeclarations", null },
        { nameof(BlueskyOAuthPermissionSets.ManageLabelerService), "app.bsky.authManageLabelerService", null },
        { nameof(BlueskyOAuthPermissionSets.ManageModeration), "app.bsky.authManageModeration", "did:web:api.bsky.app#bsky_appview" },
        { nameof(BlueskyOAuthPermissionSets.ManageNotifications), "app.bsky.authManageNotifications", "did:web:api.bsky.app#bsky_appview" },
        { nameof(BlueskyOAuthPermissionSets.ManageProfile), "app.bsky.authManageProfile", null },
        { nameof(BlueskyOAuthPermissionSets.ViewAll), "app.bsky.authViewAll", "did:web:api.bsky.app#bsky_appview" },
        { nameof(BlueskyOAuthPermissionSets.FullChatClient), "chat.bsky.authFullChatClient", "did:web:api.bsky.chat#bsky_chat" }
    };

    [Theory]
    [MemberData(nameof(PublishedPermissionSets))]
    public void PublishedSetsUseTheLexiconNsidAndRequiredAudience(string name, string nsid, string? audience)
    {
        OAuthPermissionSet permissionSet = name switch
        {
            nameof(BlueskyOAuthPermissionSets.CreatePosts) => BlueskyOAuthPermissionSets.CreatePosts,
            nameof(BlueskyOAuthPermissionSets.DeleteContent) => BlueskyOAuthPermissionSets.DeleteContent,
            nameof(BlueskyOAuthPermissionSets.FullApp) => BlueskyOAuthPermissionSets.FullApp,
            nameof(BlueskyOAuthPermissionSets.ManageFeedDeclarations) => BlueskyOAuthPermissionSets.ManageFeedDeclarations,
            nameof(BlueskyOAuthPermissionSets.ManageLabelerService) => BlueskyOAuthPermissionSets.ManageLabelerService,
            nameof(BlueskyOAuthPermissionSets.ManageModeration) => BlueskyOAuthPermissionSets.ManageModeration,
            nameof(BlueskyOAuthPermissionSets.ManageNotifications) => BlueskyOAuthPermissionSets.ManageNotifications,
            nameof(BlueskyOAuthPermissionSets.ManageProfile) => BlueskyOAuthPermissionSets.ManageProfile,
            nameof(BlueskyOAuthPermissionSets.ViewAll) => BlueskyOAuthPermissionSets.ViewAll,
            nameof(BlueskyOAuthPermissionSets.FullChatClient) => BlueskyOAuthPermissionSets.FullChatClient,
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        Assert.Equal(nsid, permissionSet.Nsid.ToString());
        Assert.Equal(audience, permissionSet.Audience);
        Assert.Equal(
            audience is null ? $"include:{nsid}" : $"include:{nsid}?aud={Uri.EscapeDataString(audience)}",
            permissionSet.ToString());
    }

    [Fact]
    public void BlueskySetsCanBeConfiguredOrPassedAsRawRequestScopes()
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json")
        {
            Scopes = ["atproto", "blob:*/*"],
            PermissionSets = [BlueskyOAuthPermissionSets.FullApp, BlueskyOAuthPermissionSets.FullChatClient]
        };

        string[] requestScopes = ["atproto", "blob:*/*", BlueskyOAuthPermissionSets.FullApp, BlueskyOAuthPermissionSets.FullChatClient];

        Assert.Equal(requestScopes, options.GetRequestedScopes());
    }
}
