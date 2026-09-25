// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class OAuthClientLoginStateTests
{
    private static OAuthClient CreateOAuthClient() =>
        new(httpClientConfigurator: httpClient => httpClient,
            innerHandlerFactory: () => new HttpClientHandler(),
            loggerFactory: null,
            options: new OAuthOptions("https://client.test/clientMetadata.json"));

    private static OAuthLoginState CreateLoginState(
        string expectedAuthority = "https://authority.test/",
        string expectedService = "https://service.test/") =>
        new(startUrl: "https://authority.test/authorize",
            state: "state",
            codeVerifier: "codeVerifier",
            redirectUri: "https://client.test/callback",
            error: string.Empty,
            errorDescription: string.Empty,
            expectedAuthority: expectedAuthority,
            expectedService: expectedService,
            proofKey: "proofKey",
            correlationId: Guid.NewGuid());

    [Fact]
    public void AFreshClientHasNoState()
    {
        Assert.Null(CreateOAuthClient().State);
    }

    [Fact]
    public void RestoringStateWithAbsoluteHttpsUrisIsAccepted()
    {
        OAuthClient client = CreateOAuthClient();

        client.State = CreateLoginState();

        Assert.NotNull(client.State);
        Assert.Equal("https://authority.test/", client.State.ExpectedAuthority);
        Assert.Equal("https://service.test/", client.State.ExpectedService);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("authority.test")]
    [InlineData("/authorize")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a uri at all")]
    public void RestoringStateWithAnExpectedAuthorityWhichIsNotAnAbsoluteHttpUriIsRejected(string expectedAuthority)
    {
        OAuthClient client = CreateOAuthClient();

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => client.State = CreateLoginState(expectedAuthority: expectedAuthority));

        Assert.Contains(nameof(OAuthLoginState.ExpectedAuthority), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("service.test")]
    [InlineData("file:///etc/passwd")]
    public void RestoringStateWithAnExpectedServiceWhichIsNotAnAbsoluteHttpUriIsRejected(string expectedService)
    {
        OAuthClient client = CreateOAuthClient();

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => client.State = CreateLoginState(expectedService: expectedService));

        Assert.Contains(nameof(OAuthLoginState.ExpectedService), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoringStateWhichIsRejectedLeavesAnyPreviouslyRestoredStateIntact()
    {
        OAuthClient client = CreateOAuthClient();

        client.State = CreateLoginState();

        Assert.Throws<ArgumentException>(
            () => client.State = CreateLoginState(expectedService: "not-a-uri"));

        Assert.NotNull(client.State);
        Assert.Equal("https://authority.test/", client.State.ExpectedAuthority);
        Assert.Equal("https://service.test/", client.State.ExpectedService);
    }

    [Fact]
    public void RestoringNullStateIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => CreateOAuthClient().State = null);
    }

    [Fact]
    public async Task ALoginWhoseArgumentsAreRejectedLeavesAnyPreviouslyRestoredStateIntact()
    {
        OAuthClient client = CreateOAuthClient();

        client.State = CreateLoginState();
        string proofKey = client.State!.ProofKey;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.BuildOAuth2LoginUri(
                service: new Uri("https://other.test/"),
                authority: new Uri("https://otherauthority.test/"),
                returnUri: new Uri("https://client.test/callback"),
                scopes: [],
                cancellationToken: TestContext.Current.CancellationToken));

        // A rejected login must not have replaced the proof key, or the authority the issued token is checked against,
        // of the login which is still in progress.
        Assert.NotNull(client.State);
        Assert.Equal(proofKey, client.State.ProofKey);
        Assert.Equal("https://authority.test/", client.State.ExpectedAuthority);
        Assert.Equal("https://service.test/", client.State.ExpectedService);
    }
}
