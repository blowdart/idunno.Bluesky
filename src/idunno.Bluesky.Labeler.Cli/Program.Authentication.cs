// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Reflection;
using System.Runtime.CompilerServices;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.OAuthCallback;

using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.Labeler.Cli;

internal static partial class Program
{
    private const string CallbackLogo = "<img src=\"data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCA2NCA2NCI+PHJlY3QgeD0iMSIgeT0iMSIgd2lkdGg9IjYyIiBoZWlnaHQ9IjYyIiByeD0iMTEiIGZpbGw9IiMzNzMwNmIiLz48cGF0aCBkPSJNMTQgMTloOGw3IDExIDctMTFoOEwzMyAzNWwxMiAxNmgtOWwtNy0xMS03IDExaC05bDEyLTE2eiIgZmlsbD0iI2Y1ZjRmZiIvPjxwYXRoIGQ9Ik00NyAyOWMtNiAwLTEwIDUtMTAgMTEgMCA3IDQgMTEgMTEgMTEgNCAwIDYtMSA4LTJ2LTZjLTMgMi01IDMtOCAzLTMgMC01LTItNS00aDE0di0zYzAtNi00LTEwLTEwLTEwem0tNCA5YzAtMyAyLTQgNC00czQgMSA0IDR6IiBmaWxsPSIjODFlOGQ1Ii8+PHBhdGggZD0iTTEzIDExaDExdjNIMTN6IiBmaWxsPSIjYTY5YWY5Ii8+PC9zdmc+\" width=\"64\" height=\"64\" alt=\"\" aria-hidden=\"true\" />";

    /// <summary>Returns the commit identifier included in the CLI assembly's informational version.</summary>
    internal static string? GetBuildCommitId()
    {
        Assembly assembly = typeof(Program).Assembly;
        string? buildCommitId = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "BuildCommitId")?.Value;
        if (!string.IsNullOrEmpty(buildCommitId))
        {
            return buildCommitId;
        }

        string? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informationalVersion is null)
        {
            return null;
        }

        int metadataStart = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        if (metadataStart < 0)
        {
            return null;
        }

        ReadOnlySpan<char> metadata = informationalVersion.AsSpan(metadataStart + 1);
        int commitLength = 0;
        while (commitLength < metadata.Length && Uri.IsHexDigit(metadata[commitLength]))
        {
            commitLength++;
        }

        return commitLength >= 7 ? metadata[..commitLength].ToString() : null;
    }

    /// <summary>Retains useful fixed OAuth failure categories without printing provider descriptions, URLs or credentials.</summary>
    internal static string ClassifyOAuthFailure(OAuthException exception)
    {
        string[] categories = ["invalid_scope", "invalid_client_metadata", "access_denied", "invalid_grant", "invalid_client", "temporarily_unavailable"];
        return categories.FirstOrDefault(category => exception.Message.Contains(category, StringComparison.Ordinal))
            ?? "authentication error";
    }

    /// <summary>Adds explicit opt-in persistence without allowing mutation confirmation to authorize credential storage.</summary>
    private static Option<bool> AddRememberMeOption(Command command)
    {
        Option<bool> option = new("--remember-me")
        {
            Description = "Save a new session for this exact account and OAuth permission set in ~/.xenon; saved sessions are reused automatically. Windows uses current-user DPAPI; other systems require confirmation to save unencrypted credentials."
        };
        command.Options.Add(option);
        return option;
    }

    /// <summary>Creates a read-only listing of saved accounts, permissions and associated labeler origins.</summary>
    private static Command CreateListCredentialsCommand()
    {
        Command command = new("list-credentials", "List saved accounts, permissions and known labeler URLs without displaying credentials.");
        command.SetAction((_, cancellationToken) => ListCredentialsAsync(
            output: Console.Out,
            cancellationToken: cancellationToken));
        return command;
    }

    /// <summary>Displays saved-session metadata without printing tokens, keys or untrusted control characters.</summary>
    /// <param name="output">The destination for the listing.</param>
    /// <param name="directory">An optional private cache directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The successful process exit code.</returns>
    internal static async Task<int> ListCredentialsAsync(
        TextWriter output,
        string? directory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        IReadOnlyList<CliCredentialCacheSummary> saved = await CliCredentialCache.ListAsync(directory, cancellationToken).ConfigureAwait(false);
        if (saved.Count == 0)
        {
            await output.WriteLineAsync("ℹ   No saved credentials found.").ConfigureAwait(false);
            return 0;
        }

        foreach (CliCredentialCacheSummary item in saved)
        {
            await output.WriteLineAsync($"👤  Account: {SanitizeConsoleText(item.Binding.Account)} ({SanitizeConsoleText(item.Binding.Actor)})").ConfigureAwait(false);
            if (item.LabelerOrigins.Count == 0)
            {
                await output.WriteLineAsync("  🔗  Labeler: unknown (not recorded)").ConfigureAwait(false);
            }
            else
            {
                foreach (string origin in item.LabelerOrigins)
                {
                    await output.WriteLineAsync($"  🔗  Labeler: {SanitizeConsoleText(origin)}").ConfigureAwait(false);
                }
            }

            await output.WriteLineAsync($"  🔒  Permissions: {SanitizeConsoleText(item.Binding.RequestedScope)}").ConfigureAwait(false);
        }

        return 0;
    }

    /// <summary>Starts a quiet loopback listener with static branding that never embeds callback credentials in its page.</summary>
    internal static Task<CallbackServer> CreateCallbackAsync(CancellationToken cancellationToken) =>
        CallbackServer.CreateAsync(
            path: null,
            loggerFactory: NullLoggerFactory.Instance,
            configure: callback =>
            {
                string? buildCommitId = GetBuildCommitId();
                string buildIdentifier = buildCommitId is null
                    ? string.Empty
                    : $"<p id=\"callback-build\" style=\"text-align:right;font-size:.75rem\">Build <code>{buildCommitId}</code></p>";

                callback.SuccessTitle = "<title>Xenon sign-in</title>";
                callback.SuccessBody = $"<p aria-hidden=\"true\">&nbsp;</p><h1>{CallbackLogo}Xenon sign-in received</h1><p>Return to the Xenon CLI to check whether authentication and your command succeeded. You can close this tab.</p>{buildIdentifier}";
                callback.FailureTitle = "<title>Xenon sign-in failed</title>";
                callback.FailureBody = $"<p aria-hidden=\"true\">&nbsp;</p><h1>{CallbackLogo}Xenon sign-in was not completed</h1><p>Return to the Xenon CLI for the result. No further workflow steps will run without successful authentication.</p>{buildIdentifier}";
            },
            cancellationToken: cancellationToken);

    /// <summary>Separates an expired browser wait from caller cancellation without exposing the callback URL or OAuth response.</summary>
    internal static async Task<string?> WaitForSignInCallbackAsync(
        Func<CancellationToken, Task<string>> wait,
        TextWriter diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            return await wait(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await diagnostics.WriteLineAsync("⚠   OAuth sign-in timed out. No further workflow steps will run; run the command again when ready to sign in.").ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Stops if the platform browser launcher fails without writing its secret-bearing command arguments.</summary>
    internal static async Task<bool> OpenSignInBrowserAsync(Action open, TextWriter diagnostics)
    {
        try
        {
            open();
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            await diagnostics.WriteLineAsync("✖   The browser could not be opened. Check your default browser or platform browser launcher. No further workflow steps will run.").ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>Requires a separate human decision for plaintext persistence, never mutation flags or redirected answers.</summary>
    internal static async Task<bool> ConfirmCredentialStorageAsync(
        TextReader input,
        TextWriter diagnostics,
        bool inputRedirected,
        CancellationToken cancellationToken)
    {
        if (inputRedirected)
        {
            await diagnostics.WriteLineAsync("⚠   Unencrypted credential storage requires interactive consent. Neither --yes nor redirected input authorizes it.").ConfigureAwait(false);
            return false;
        }

        await diagnostics.WriteAsync("⚠   Save unencrypted OAuth credentials in your user-only ~/.xenon directory? [y/N] ").ConfigureAwait(false);
        string? answer = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Keeps account-purpose warnings visible while restricting sign-in progress to verbose output.</summary>
    internal static async Task WriteSignInNoticeAsync(
        TextWriter diagnostics,
        Handle handle,
        string? labelerPurpose,
        bool rememberMe,
        bool verbose = false)
    {
        if (verbose)
        {
            await diagnostics.WriteLineAsync($"🔑  Xenon starting sign-in for {handle}.").ConfigureAwait(false);
        }
        if (labelerPurpose is not null)
        {
            await diagnostics.WriteLineAsync($"⚠   {labelerPurpose}").ConfigureAwait(false);
        }
        if (verbose)
        {
            await diagnostics.WriteLineAsync(rememberMe
                ? "💾  Credential persistence was requested with --remember-me."
                : "ℹ   Reusing saved credentials when available; otherwise this sign-in is temporary. Use --remember-me to save a new session.").ConfigureAwait(false);
        }
    }

    /// <summary>Authenticates only the named account, warning for a labeler-account purpose, and stops on denial or an incomplete callback.</summary>
    private static async Task<bool> SignInAsync(
        AtProtoAgent agent,
        Handle handle,
        OAuthOptions options,
        string? purpose,
        bool rememberMe,
        Uri? labelerOrigin,
        CliSignInLifetime lifetime,
        CancellationToken cancellationToken)
    {
        await WriteSignInNoticeAsync(
            diagnostics: Console.Error,
            handle: handle,
            labelerPurpose: purpose,
            rememberMe: rememberMe,
            verbose: s_verboseOutput.Value).ConfigureAwait(false);
        Did expectedDid = await agent.ResolveHandle(handle, cancellationToken).ConfigureAwait(false)
            ?? throw new OAuthException("The selected account could not be resolved.");
        Uri expectedPds = await agent.ResolvePds(expectedDid, cancellationToken).ConfigureAwait(false)
            ?? throw new OAuthException("The selected account's PDS could not be resolved.");
        CliCredentialCache? cache = await lifetime.GetCacheAsync(
            binding: new(
                Account: handle.ToString(),
                Actor: expectedDid.Value,
                ClientMetadataId: options.ClientId,
                RequestedScope: string.Join(" ", options.Scopes),
                Service: expectedPds),
            rememberMe: rememberMe,
            cancellationToken: cancellationToken,
            labelerOrigin: labelerOrigin).ConfigureAwait(false);
        if (cache is not null)
        {
            cache.Attach(agent);
            DPoPAccessCredentials? saved = await cache.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (saved is not null)
            {
                await WriteVerboseNoticeAsync($"🔒  Validating the remembered session for {handle} with its PDS.").ConfigureAwait(false);
                if (!await agent.Login(saved, cancellationToken).ConfigureAwait(false) ||
                    !await agent.RefreshCredentials(cancellationToken).ConfigureAwait(false) ||
                    !agent.IsAuthenticated || agent.Did != expectedDid)
                {
                    await Console.Error.WriteLineAsync("✖   The remembered session could not be renewed. No command was sent. Revoke or remove this saved session before signing in again.").ConfigureAwait(false);
                    return false;
                }

                await WriteVerboseNoticeAsync($"✔   Remembered sign-in validated for {handle}. The command has not yet completed.").ConfigureAwait(false);
                return true;
            }
        }
        CallbackServer callback = await CreateCallbackAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable callbackLifetime = callback.ConfigureAwait(false);
        OAuthClient loginClient = agent.CreateOAuthClient();
        Uri loginUri = await agent.BuildOAuth2LoginUri(
            oAuthClient: loginClient,
            handle: handle,
            scopes: options.Scopes,
            returnUri: callback.Uri,
            allowLoopback: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await Console.Error.WriteLineAsync($"🌐  Opening the browser for {handle}. Cancel in the terminal to abort.").ConfigureAwait(false);
        if (!await OpenSignInBrowserAsync(() => OAuthClient.OpenBrowser(loginUri), Console.Error).ConfigureAwait(false))
        {
            return false;
        }
        string? callbackData = await WaitForSignInCallbackAsync(
            wait: token => callback.WaitForCallbackAsync(cancellationToken: token),
            diagnostics: Console.Error,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (callbackData is null)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(callbackData) ||
            !await agent.ProcessOAuth2LoginResponse(loginClient, callbackData, cancellationToken).ConfigureAwait(false) ||
            !agent.IsAuthenticated || agent.Did != expectedDid)
        {
            await Console.Error.WriteLineAsync("✖   OAuth sign-in was denied or incomplete. No further workflow steps will run.").ConfigureAwait(false);
            return false;
        }
        if (cache is not null && agent.Credentials is { } credentials)
        {
            await cache.SaveAsync(credentials, cancellationToken).ConfigureAwait(false);
        }

        await WriteVerboseNoticeAsync($"✔   Signed in as {handle}. Authentication succeeded; the command has not yet completed.").ConfigureAwait(false);
        return true;
    }

    /// <summary>Keeps cache leases until all agents in the workflow are disposed, serializing single-use refresh tokens across processes.</summary>
    /// <param name="directory">An optional private cache directory.</param>
    internal sealed class CliSignInLifetime(string? directory = null) : IAsyncDisposable
    {
        private readonly Dictionary<CliCredentialCacheBinding, CliCredentialCache> _caches = [];
        private bool? _storagePermitted;

        /// <summary>Obtains one exact session lease and asks separately before any unencrypted persistence.</summary>
        internal async Task<CliCredentialCache?> GetCacheAsync(
            CliCredentialCacheBinding binding,
            bool rememberMe,
            CancellationToken cancellationToken,
            Uri? labelerOrigin = null)
        {
            if (_caches.TryGetValue(binding, out CliCredentialCache? existing))
            {
                existing.AssociateLabelerOrigin(labelerOrigin);
                return existing;
            }
            CliCredentialCache cache = new(
                binding: binding,
                directory: directory,
                allowUnencryptedStorage: true,
                labelerOrigin: labelerOrigin);
            if (!cache.HasSavedSession)
            {
                if (!rememberMe)
                {
                    await cache.DisposeAsync().ConfigureAwait(false);
                    return null;
                }

                _storagePermitted ??= await CliCredentialCache.ConfirmStorageAsync(
                    warning: Console.Error,
                    confirm: token => ConfirmCredentialStorageAsync(
                        input: Console.In,
                        diagnostics: Console.Error,
                        inputRedirected: Console.IsInputRedirected,
                        cancellationToken: token),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!_storagePermitted.Value)
                {
                    await cache.DisposeAsync().ConfigureAwait(false);
                    await Console.Error.WriteLineAsync("⚠   Credential storage was declined. Continuing with a temporary sign-in.").ConfigureAwait(false);
                    return null;
                }
            }

            _caches.Add(binding, cache);
            await cache.AcquireSessionAsync(cancellationToken).ConfigureAwait(false);
            await WriteVerboseNoticeAsync("💾  Saving credentials in ~/.xenon.").ConfigureAwait(false);
            return cache;
        }

        /// <summary>Detaches credential-update subscribers and releases each retained process lease.</summary>
        public async ValueTask DisposeAsync()
        {
            foreach (CliCredentialCache cache in _caches.Values)
            {
                await cache.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
