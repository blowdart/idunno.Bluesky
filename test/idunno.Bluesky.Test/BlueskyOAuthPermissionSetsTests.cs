// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;
using idunno.Bluesky.Authentication;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class BlueskyOAuthPermissionSetsTests
{
    public static TheoryData<OAuthPermissionSet, string, string?> PublishedPermissionSets => new()
    {
        { BlueskyOAuthPermissionSets.CreatePosts, "app.bsky.authCreatePosts", "did:web:api.bsky.app#bsky_appview" },
        { BlueskyOAuthPermissionSets.DeleteContent, "app.bsky.authDeleteContent", null },
        { BlueskyOAuthPermissionSets.FullApp, "app.bsky.authFullApp", "did:web:api.bsky.app#bsky_appview" },
        { BlueskyOAuthPermissionSets.ManageFeedDeclarations, "app.bsky.authManageFeedDeclarations", null },
        { BlueskyOAuthPermissionSets.ManageLabelerService, "app.bsky.authManageLabelerService", null },
        { BlueskyOAuthPermissionSets.ManageModeration, "app.bsky.authManageModeration", "did:web:api.bsky.app#bsky_appview" },
        { BlueskyOAuthPermissionSets.ManageNotifications, "app.bsky.authManageNotifications", "did:web:api.bsky.app#bsky_appview" },
        { BlueskyOAuthPermissionSets.ManageProfile, "app.bsky.authManageProfile", null },
        { BlueskyOAuthPermissionSets.ViewAll, "app.bsky.authViewAll", "did:web:api.bsky.app#bsky_appview" },
        { BlueskyOAuthPermissionSets.FullChatClient, "chat.bsky.authFullChatClient", "did:web:api.bsky.chat#bsky_chat" }
    };

    [Theory]
    [MemberData(nameof(PublishedPermissionSets))]
    public void PublishedSetsUseTheLexiconNsidAndRequiredAudience(OAuthPermissionSet permissionSet, string nsid, string? audience)
    {
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
