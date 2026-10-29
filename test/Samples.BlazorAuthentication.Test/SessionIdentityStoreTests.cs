// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Authentication;
using System.Security.Claims;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

namespace Samples.BlazorAuthentication.Test;

public class SessionIdentityStoreTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CredentialCommitAndSessionMutationsAreAtomic(bool replacement, bool mutationFirst)
    {
        const string did = "did:plc:ewvi7nxzyoun6zhxrhs64oiz";
        const string accessToken = "eyJhbGciOiJub25lIn0.eyJzdWIiOiJkaWQ6cGxjOmV3dmk3bnh6eW91bjZ6aHhyaHM2NG9peiIsImV4cCI6NDEwMjQ0NDgwMH0.";
        var credentials = new DPoPAccessCredentials(new Uri("https://pds.example"), accessToken,
            "refresh", JsonWebKeys.CreateRsaJson(), "nonce");
        ClaimsIdentity original = IIdentityStore.BuildClaimsIdentity(credentials);
        var newer = new ClaimsIdentity(original);
        newer.RemoveClaim(newer.FindFirst(AtProtoClaims.DPoPProof)!);
        newer.AddClaim(new(AtProtoClaims.DPoPProof, JsonWebKeys.CreateRsaJson()));
        var inner = new PausingStore();
        using var store = new SessionIdentityStore(inner);
        await store.Add(original, TestContext.Current.CancellationToken);

        Task Mutate() => replacement
            ? store.Add(newer, TestContext.Current.CancellationToken)
            : store.Remove(new Did(did), TestContext.Current.CancellationToken);

        if (mutationFirst)
        {
            await Mutate();
            await Assert.ThrowsAsync<AuthenticationException>(() => store.UpdateIfNewer(credentials, TestContext.Current.CancellationToken));
        }
        else
        {
            inner.PauseRead = true;
            Task<bool> update = store.UpdateIfNewer(credentials, TestContext.Current.CancellationToken);
            await inner.ReadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            Task mutation = Mutate();
            Assert.False(mutation.IsCompleted);
            inner.ResumeRead.SetResult();
            Assert.True(await update);
            await mutation;
        }

        ClaimsIdentity? current = await store.GetIdentity(new Did(did), TestContext.Current.CancellationToken);
        Assert.Equal(replacement ? newer.FindFirst(AtProtoClaims.DPoPProof)!.Value : null,
            current?.FindFirst(AtProtoClaims.DPoPProof)?.Value);
    }

    // Pause the underlying read to force the former check/write race deterministically.
    private sealed class PausingStore : IIdentityStore
    {
        private ClaimsIdentity? _identity;
        internal bool PauseRead { get; set; }
        internal TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ResumeRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IdentityStoreEvents Events { get; set; } = new();

        public Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
        {
            _identity = new ClaimsIdentity(claimsIdentity);
            return Task.CompletedTask;
        }

        public async Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default)
        {
            if (PauseRead)
            {
                PauseRead = false;
                ReadStarted.SetResult();
                await ResumeRead.Task.WaitAsync(cancellationToken);
            }

            return _identity is null ? null : new ClaimsIdentity(_identity);
        }

        public Task Remove(Did did, CancellationToken cancellationToken = default)
        {
            _identity = null;
            return Task.CompletedTask;
        }

        public Task Update(ClaimsIdentity identity, CancellationToken cancellationToken) => Add(identity, cancellationToken);
        public Task<string?> StartRefresh(Did did, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<bool> EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsRefreshing(Did did, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
