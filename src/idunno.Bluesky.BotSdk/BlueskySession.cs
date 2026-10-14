// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;

using idunno.Bluesky.BotSdk.DataProviders;

namespace idunno.Bluesky.BotSdk;

// Prefer restoring and refreshing a saved session to repeated createSession calls, which have
// their own account limits. The sender is the single caller of this long-lived session wrapper.
/// <summary>Restores encrypted refresh credentials and reuses one authenticated agent across action attempts.</summary>
/// <param name="agent">The long-lived protected SDK client owned by this session.</param>
/// <param name="database">The chosen provider storing encrypted refresh credentials.</param>
/// <param name="vault">The credential encryption helper.</param>
/// <param name="options">The expected account identity and login configuration.</param>
/// <param name="logger">The authentication diagnostic logger.</param>
internal sealed class BlueskySession(
    BlueskyAgent agent,
    IDatabaseProvider database,
    CredentialVault vault,
    BotOptions options,
    ILogger<BlueskySession> logger) : IDisposable
{
    private bool _credentialsPersistencePending;
    public BlueskyAgent Agent { get; } = agent;
    public bool RefreshRequired { get; set; }

    /// <summary>Restores or refreshes the saved session before creating a new login and verifies the expected DID.</summary>
    /// <param name="cancellationToken">The active ownership/shutdown token.</param>
    public async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        Agent.CredentialsUpdatedAsync = async (args, token) =>
        {
            // Refresh tokens rotate. Persist the latest credentials at the SDK's notification
            // boundary; if storage fails, remember to retry the current in-memory credentials.
            _credentialsPersistencePending = true;
            await database.SaveCredentialsAsync(vault.Encrypt(args.AccessCredentials), token).ConfigureAwait(false);
            _credentialsPersistencePending = false;
            BotLog.CredentialsSaved(logger, args.Did.ToString());
        };

        if (Agent.IsAuthenticated)
        {
            if (_credentialsPersistencePending)
            {
                // Save before another refresh can rotate the token again. This repairs a storage
                // outage while the process survives; a crash in that gap may require a fresh login.
                await database.SaveCredentialsAsync(
                    vault.Encrypt(Agent.Credentials
                    ?? throw new InvalidOperationException("Authenticated without credentials.")),
                    cancellationToken).ConfigureAwait(false);
                _credentialsPersistencePending = false;
            }

            if (RefreshRequired || Agent.Credentials?.ExpiresOn <= DateTimeOffset.UtcNow.AddMinutes(2))
            {
                // Refresh before expiry and after a retryable 401, not on every reply.
                if (!await Agent.RefreshCredentials(cancellationToken).ConfigureAwait(false))
                {
                    await database.SaveCredentialsAsync(null, cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("Session refresh was rejected. Check the bot account and its app password.");
                }

                await database.SaveCredentialsAsync(
                    vault.Encrypt(Agent.Credentials
                    ?? throw new InvalidOperationException("Refreshed without credentials.")),
                    cancellationToken).ConfigureAwait(false);
                _credentialsPersistencePending = false;
                RefreshRequired = false;
            }

            return;
        }

        bool restored = false;
        byte[]? encrypted = await database.LoadCredentialsAsync(cancellationToken).ConfigureAwait(false);

        if (encrypted is not null)
        {
            restored = await Agent.RefreshCredentials(vault.Decrypt(encrypted), cancellationToken).ConfigureAwait(false);

            if (!restored)
            {
                // An explicitly rejected stored token can require a new login. Transport failures
                // propagate instead of being mistaken for proof that the saved session is invalid.
                await database.SaveCredentialsAsync(null, cancellationToken).ConfigureAwait(false);
                BotLog.Recovering(logger, "sender", "stored refresh token rejected; creating a new session", DateTimeOffset.UtcNow);
            }
        }

        if (!restored)
        {
            AtProtoHttpResult<bool> result = await Agent.Login(options.Handle, options.AppPassword, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded || !result.Result)
            {
                if (result.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException("Bot login was rejected. Check Bot:Handle and Bot:AppPassword.");
                }

                throw new RemoteFailureException(result.StatusCode, result.AtErrorDetail?.Error, "authenticating the bot account");
            }
        }

        if (Agent.Did?.ToString() != options.Did)
        {
            // A handle is a login aid, not the durable account identity. Refuse a different DID
            // before any reply is sent, even if those credentials authenticated successfully.
            throw new InvalidOperationException("The authenticated account does not match Bot:Did.");
        }

        // Login also invokes the callback, but explicitly await storage here even if an SDK login notification failed.
        await database.SaveCredentialsAsync(
            vault.Encrypt(Agent.Credentials
            ?? throw new InvalidOperationException("Login succeeded without credentials.")),
            cancellationToken).ConfigureAwait(false);
        BotLog.Authenticated(logger, options.Did, restored);
    }

    /// <inheritdoc/>
    public void Dispose() => Agent.Dispose();
}
