// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class OAuthOptionsScopesTests
{
    private static OAuthOptions CreateOptions() => new("https://client.test/clientMetadata.json");

    [Fact]
    public void ScopesDoNotAliasTheCollectionAssignedToThem()
    {
        List<string> scopes = ["atproto", "transition:generic"];

        OAuthOptions options = new("https://client.test/clientMetadata.json")
        {
            Scopes = scopes
        };

        scopes.Add("transition:chat.bsky");
        scopes.Remove("atproto");

        Assert.Equal(["atproto", "transition:generic"], options.Scopes);
    }

    [Fact]
    public void ScopesAreDeduplicatedWhenAssigned()
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json")
        {
            Scopes = ["atproto", "atproto", "transition:generic"]
        };

        Assert.Equal(["atproto", "transition:generic"], options.Scopes);
    }

    [Fact]
    public void ScopesAreOnlyEnumeratedOnceWhenAssigned()
    {
        int enumerationCount = 0;

        IEnumerable<string> CountingScopes()
        {
            enumerationCount++;
            yield return "atproto";
        }

        OAuthOptions options = new("https://client.test/clientMetadata.json")
        {
            Scopes = CountingScopes()
        };

        _ = options.Scopes.ToList();
        _ = options.Scopes.ToList();

        Assert.Equal(1, enumerationCount);
    }

    [Fact]
    public void AssigningNullScopesThrowsArgumentNullException()
    {
        OAuthOptions options = CreateOptions();

        Assert.Throws<ArgumentNullException>(() => options.Scopes = null!);
    }

    [Fact]
    public void AssigningAnEmptyScopeCollectionThrowsArgumentOutOfRangeException()
    {
        OAuthOptions options = CreateOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Scopes = []);
    }

    [Fact]
    public void PermissionSetsAreEmptyByDefaultAndDoNotChangeRawScopes()
    {
        OAuthOptions options = CreateOptions();

        Assert.Empty(options.PermissionSets);
        Assert.Equal(["atproto"], options.GetRequestedScopes());

        options.Scopes = ["transition:generic", "custom:scope"];

        Assert.Equal(options.Scopes, options.GetRequestedScopes());
    }

    [Fact]
    public void ConfiguredScopesCombineAndDeduplicateRawScopesAndPermissionSets()
    {
        OAuthOptions options = CreateOptions();
        options.Scopes = ["atproto", "include:com.example.authBasic", "blob:image/*"];
        options.PermissionSets = [new("com.example.authBasic"), new("com.example.authOther"), new("com.example.authOther")];

        Assert.Equal(["atproto", "include:com.example.authBasic", "blob:image/*", "include:com.example.authOther"], options.GetRequestedScopes());
        Assert.Equal(["atproto", "include:com.example.authBasic", "blob:image/*"], options.Scopes);
    }

    [Fact]
    public void PermissionSetsAreCopiedAndOnlyEnumeratedOnce()
    {
        int enumerationCount = 0;
        List<OAuthPermissionSet> permissionSets = [new("com.example.authBasic")];

        IEnumerable<OAuthPermissionSet> CountingPermissionSets()
        {
            enumerationCount++;
            foreach (OAuthPermissionSet permissionSet in permissionSets)
            {
                yield return permissionSet;
            }
        }

        OAuthOptions options = CreateOptions();
        options.PermissionSets = CountingPermissionSets();
        IEnumerable<string> snapshot = options.GetRequestedScopes();
        permissionSets.Clear();

        Assert.Equal(["atproto", "include:com.example.authBasic"], options.GetRequestedScopes());
        Assert.Equal(snapshot, options.GetRequestedScopes());
        Assert.Equal(1, enumerationCount);

        options.PermissionSets = [];

        Assert.Equal(["atproto"], options.GetRequestedScopes());
        Assert.Equal(["atproto", "include:com.example.authBasic"], snapshot);
    }

    [Fact]
    public void NullPermissionSetsOrEntriesAreRejectedWithoutChangingConfiguration()
    {
        OAuthOptions options = CreateOptions();
        options.PermissionSets = [new("com.example.authBasic")];

        Assert.Throws<ArgumentNullException>(() => options.PermissionSets = null!);
        Assert.Throws<ArgumentNullException>(() => options.PermissionSets = [null!]);
        Assert.Equal(["atproto", "include:com.example.authBasic"], options.GetRequestedScopes());
    }
}
