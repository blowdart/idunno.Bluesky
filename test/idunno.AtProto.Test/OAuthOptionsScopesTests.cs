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
}
