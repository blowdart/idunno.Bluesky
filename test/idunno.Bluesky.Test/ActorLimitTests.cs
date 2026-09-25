// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class ActorLimitTests
{
    private static readonly Uri s_service = new("https://test.internal");

    [Fact]
    public async Task GetProfilesRejectsMoreActorsThanTheMaximum()
    {
        using HttpClient httpClient = new();

        List<AtIdentifier> actors =
            [.. Enumerable.Range(0, Maximum.ProfilesToGet + 1).Select(index => (AtIdentifier)new Did($"did:plc:identifier{index}"))];

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => BlueskyServer.GetProfiles(actors, s_service, accessCredentials: null, httpClient: httpClient, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchActorsRejectsALimitOverTheMaximum()
    {
        using HttpClient httpClient = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => BlueskyServer.SearchActors("query", Maximum.ActorSearchResults + 1, cursor: null, s_service, accessCredentials: null, httpClient: httpClient, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchActorsTypeaheadRejectsALimitOverTheMaximum()
    {
        using HttpClient httpClient = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => BlueskyServer.SearchActorsTypeahead("query", Maximum.ActorTypeaheadSearchResults + 1, s_service, accessCredentials: null, httpClient: httpClient, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(nameof(Maximum.ProfilesToGet), 25)]
    [InlineData(nameof(Maximum.ActorSearchResults), 100)]
    [InlineData(nameof(Maximum.ActorTypeaheadSearchResults), 100)]
    [InlineData(nameof(Maximum.InterestTags), 100)]
    [InlineData(nameof(Maximum.MutedWordLengthInBytes), 10000)]
    [InlineData(nameof(Maximum.MutedWordLengthInGraphemes), 1000)]
    public void ActorMaximumsMatchTheLexicon(string name, int expected)
    {
        int actual = (int)typeof(Maximum).GetField(name)!.GetValue(null)!;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task SetContentVisibilityDeclarationRejectsANullDeclaration()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => agent.SetContentVisibilityDeclaration((ContentVisibilityDeclaration)null!, TestContext.Current.CancellationToken));
    }
}
