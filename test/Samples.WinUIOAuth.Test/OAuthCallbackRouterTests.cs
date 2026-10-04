// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Time.Testing;

using Samples.WinUIOAuth;

namespace Samples.WinUIOAuth.Test;

public class OAuthCallbackRouterTests
{
    private const string ValidCallback = "dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=code";

    [Theory]
    [InlineData(ValidCallback)]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example%2F&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&error=access_denied")]
    public void MatchingCallbackReturnsOriginalStateOnlyOnce(string callback)
    {
        OAuthCallbackRouter router = new();
        OAuthLoginState state = CreateState();
        router.Begin(state);

        Assert.Same(state, router.Take(new Uri(callback)));
        Assert.Throws<InvalidOperationException>(() => router.Take(new Uri(callback)));
    }

    [Theory]
    [InlineData("dev.idunno.bluesky://callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=code")]
    [InlineData("dev.idunno.bluesky:/other?state=expected&iss=https%3A%2F%2Fissuer.example&code=code")]
    [InlineData("dev.idunno.bluesky:/%63allback?state=expected&iss=https%3A%2F%2Fissuer.example&code=code")]
    [InlineData("https://example.org/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=code#fragment")]
    [InlineData("dev.idunno.bluesky:/callback?state=wrong&iss=https%3A%2F%2Fissuer.example&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fevil.example&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example%2Fother&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example%2F%23fragment&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fuser%40issuer.example&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=http%3A%2F%2Fissuer.example&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&error=")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=code&error=access_denied")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&state=expected&iss=https%3A%2F%2Fissuer.example&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&%73tate=expected&iss=https%3A%2F%2Fissuer.example&code=code")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=%GG")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=%")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=%FF")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=%C0%AF")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=%0A")]
    [InlineData("dev.idunno.bluesky:/callback?state=expected&iss=https%3A%2F%2Fissuer.example&code=code&malformed")]
    [InlineData("dev.idunno.bluesky:/callback")]
    public void InvalidCallbacksAreRejectedWithoutConsumingPendingLogin(string callback)
    {
        OAuthCallbackRouter router = new();
        OAuthLoginState state = CreateState();
        router.Begin(state);

        Assert.Throws<InvalidOperationException>(() => router.Take(new Uri(callback)));
        Assert.Same(state, router.Take(new Uri(ValidCallback)));
    }

    [Fact]
    public void ColdStartAndCanceledLoginRejectCallbacks()
    {
        OAuthCallbackRouter router = new();
        Assert.Throws<InvalidOperationException>(() => router.Take(new Uri(ValidCallback)));

        router.Begin(CreateState());
        router.Clear();
        Assert.Throws<InvalidOperationException>(() => router.Take(new Uri(ValidCallback)));
    }

    [Fact]
    public void ExpiredLoginCannotBeResumed()
    {
        FakeTimeProvider timeProvider = new();
        OAuthCallbackRouter router = new(timeProvider);
        router.Begin(CreateState());
        timeProvider.Advance(OAuthCallbackRouter.LoginLifetime);

        Assert.Throws<InvalidOperationException>(() => router.Take(new Uri(ValidCallback)));
        router.Begin(CreateState());
        Assert.NotNull(router.Take(new Uri(ValidCallback)));
    }

    [Fact]
    public void StartingAnotherLoginCannotReplacePendingState()
    {
        OAuthCallbackRouter router = new();
        OAuthLoginState state = CreateState();
        router.Begin(state);

        Assert.Throws<InvalidOperationException>(() => router.Begin(CreateState()));
        Assert.Same(state, router.Take(new Uri(ValidCallback)));
    }

    [Fact]
    public void OversizedCallbacksAreRejected()
    {
        OAuthCallbackRouter router = new();
        router.Begin(CreateState());

        Assert.Throws<InvalidOperationException>(() => router.Take(new Uri(ValidCallback + "&extra=" + new string('a', 16384))));
    }

    [Fact]
    public void ExcessiveParametersAreRejected()
    {
        OAuthCallbackRouter router = new();
        router.Begin(CreateState());
        string extras = string.Concat(Enumerable.Range(0, 32).Select(i => $"&extra{i}=value"));

        Assert.Throws<InvalidOperationException>(() => router.Take(new Uri(ValidCallback + extras)));
    }

    [Fact]
    public void LoginWithDifferentRedirectIsRejected()
    {
        OAuthCallbackRouter router = new();
        OAuthLoginState state = CreateState();
        state.RedirectUri = "https://example.org/callback";

        Assert.Throws<InvalidOperationException>(() => router.Begin(state));
    }

    private static OAuthLoginState CreateState() => new(
        startUrl: "https://issuer.example/authorize",
        state: "expected",
        codeVerifier: "in-memory-verifier",
        redirectUri: OAuthCallbackRouter.RedirectUri,
        error: string.Empty,
        errorDescription: string.Empty,
        expectedAuthority: "https://issuer.example/",
        expectedService: "https://pds.example",
        proofKey: "in-memory-key",
        correlationId: Guid.NewGuid());
}
