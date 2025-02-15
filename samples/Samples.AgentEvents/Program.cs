// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;

using Samples.Common;

namespace Samples.AgentEvents
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(PerformOperations);
            await parser.InvokeAsync(args);

            return 0;
        }

        static async Task PerformOperations(string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(handle);
            ArgumentException.ThrowIfNullOrEmpty(password);

            // This approximates a token store for the purposes of the sample.
            StoredAuthenticationState? persistedLoginState = null;

            // Uncomment the next line to route all requests through Fiddler Everywhere
            // proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // If a proxy is being used turn off certificate revocation checks.
            //
            // WARNING: this setting can introduce security vulnerabilities.
            // The assumption in these samples is that any proxy is a debugging proxy,
            // which tend to not support CRLs in the proxy HTTPS certificates they generate.
            bool checkCertificateRevocationList = true;
            if (proxyUri is not null)
            {
                checkCertificateRevocationList = false;
            }

            // Setting logs to error only so you can see the event output.
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Error))
            {
                using (var agent = new AtProtoAgent(new Uri("https://bsky.social"),
                    options: new AtProtoAgentOptions
                    {
                        LoggerFactory = loggerFactory,
                        HttpClientOptions = new HttpClientOptions()
                        {
                            ProxyUri = proxyUri,
                            CheckCertificateRevocationList = checkCertificateRevocationList
                        }
                    }))
                {
                    agent.Authenticated += (sender, e) =>
                    {
                        // Here you would save the DID, refresh token and service to the appropriate secure
                        // storage location for your platform (Windows Credential store for Windows,
                        // Keyring for Apple platforms, and for Linux the user's profile directory and hope
                        // the permissions are correct.
                        //
                        // Saving the access token is optional, as is saving the authentication type.
                        //
                        // When your app restarts you can then use the RestoreSession() method on the Agent
                        // to try to restore the session you had.
                        //
                        // If you save the access token RestoreSession() will try to use that first, before
                        // falling back to using the refresh token to create a new authenticated session.
                        persistedLoginState = new StoredAuthenticationState(
                            authenticationType: e.AccessCredentials.AuthenticationType,
                            did: e.AccessCredentials.Did,
                            service: e.AccessCredentials.Service,
                            accessToken: e.AccessCredentials.AccessJwt,
                            refreshToken: e.AccessCredentials.RefreshToken);

                        if (e.AccessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
                        {
                            persistedLoginState.DPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                            persistedLoginState.DPoPNonce = dPoPAccessCredentials.DPoPNonce;
                        }

                        Console.WriteLine($"EVENT: {e.AccessCredentials.Did} authenticated on {e.AccessCredentials.Service}");
                    };

                    agent.CredentialsUpdated += (sender, e) =>
                    {
                        // Here you update your saved state. Saving the access token is optional.
                        //
                        // When your app restarts you can then use the RefreshCredentials(AtProtoCredential, CancellationToken) method on the Agent
                        // to try to restore the session you had.
                        persistedLoginState = new StoredAuthenticationState(
                            authenticationType: e.AccessCredentials.AuthenticationType,
                            did: e.AccessCredentials.Did,
                            service: e.AccessCredentials.Service,
                            accessToken: e.AccessCredentials.AccessJwt,
                            refreshToken: e.AccessCredentials.RefreshToken);

                        if (e.AccessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
                        {
                            persistedLoginState.DPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                            persistedLoginState.DPoPNonce = dPoPAccessCredentials.DPoPNonce;
                        }

                        Console.WriteLine($"EVENT: Credentials updated for : {e.AccessCredentials.Did}");
                    };

                    agent.Unauthenticated += (sender, e) =>
                    {
                        // Here you would clear any saved authentication state for the DID.
                        persistedLoginState = null;

                        Console.WriteLine($"EVENT: {e.Did} logged out from {e.Service}.");
                    };

                    agent.TokenRefreshFailed += (sender, e) =>
                    {
                        // Here you would clear any saved authentication state for the DID because the session
                        // could not refresh and so any tokens you saved will be invalid.
                        persistedLoginState = null;

                        Console.WriteLine($"EVENT: Token Refresh failed API returned {e.StatusCode}");

                        if (e.Error is not null)
                        {
                            Console.WriteLine($"       {e.Error.Error}");

                            if (!string.IsNullOrEmpty(e.Error.Message))
                            {
                                Console.WriteLine($"       {e.Error.Message}");
                            }
                        }
                    };

                    Console.WriteLine("Logging in");

                    var loginResult = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken).ConfigureAwait(false);

                    Console.WriteLine($"Login result: {loginResult.StatusCode}");

                    Console.WriteLine("Refreshing credentials");

                    bool refreshSessionResult = await agent.RefreshCredentials(cancellationToken: cancellationToken);

                    Console.WriteLine($"Refresh credentials result: {refreshSessionResult}");

                    Console.WriteLine("Logging out");

                    await agent.Logout(cancellationToken: cancellationToken);

                    Console.WriteLine("Refreshing the credentials once the agent has logged out.");

                    // We know this will throw an exception.
                    try
                    {
                        _ = await agent.RefreshCredentials(cancellationToken: cancellationToken);
                    }
                    catch (AuthenticationRequiredException e)
                    {
                        Console.WriteLine($"Exception Thrown : {e.Message}");
                    }

                    // Login again
                    _ = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Now mess with the refresh token to cause the refresh call to fail
                    if (agent.Credentials is not null)
                    {
                        // First let's copy the persisted token, because when the refresh fails it'll clear it.
                        StoredAuthenticationState? copiedLoginState = persistedLoginState;

                        agent.Credentials.RefreshToken = "invalid";

                        Console.WriteLine("Refreshing the credentials with an invalid refresh token.");
                        await agent.RefreshCredentials(cancellationToken: cancellationToken);

                        // And now restore that good persisted state and pretend the refresh fail never happened.
                        persistedLoginState = copiedLoginState;
                    }
                }

                // Now let's try to recreate credentials from the persisted state
                if (persistedLoginState is null)
                {
                    Console.WriteLine("❌\tNo persisted state to restore");
                    return;
                }

                using (var agent = new BlueskyAgent(
                    options: new BlueskyAgentOptions
                    {
                        LoggerFactory = loggerFactory,
                        HttpClientOptions = new HttpClientOptions()
                        {
                            ProxyUri = proxyUri,
                            CheckCertificateRevocationList = checkCertificateRevocationList
                        }
                    }))
                {
                    agent.Authenticated += (sender, e) =>
                    {
                        // Here you would save the DID, refresh token and service to the appropriate secure
                        // storage location for your platform (Windows Credential store for Windows,
                        // Keyring for Apple platforms, and for Linux the user's profile directory and hope
                        // the permissions are correct.
                        //
                        // Saving the access token is optional, as is saving the authentication type.
                        //
                        // When your app restarts you can then use the RestoreSession() method on the Agent
                        // to try to restore the session you had.
                        //
                        // If you save the access token RestoreSession() will try to use that first, before
                        // falling back to using the refresh token to create a new authenticated session.
                        persistedLoginState = new StoredAuthenticationState(
                            authenticationType: e.AccessCredentials.AuthenticationType,
                            did: e.AccessCredentials.Did,
                            service: e.AccessCredentials.Service,
                            accessToken: e.AccessCredentials.AccessJwt,
                            refreshToken: e.AccessCredentials.RefreshToken);

                        if (e.AccessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
                        {
                            persistedLoginState.DPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                            persistedLoginState.DPoPNonce = dPoPAccessCredentials.DPoPNonce;
                        }

                        Console.WriteLine($"EVENT: {e.AccessCredentials.Did} authenticated on {e.AccessCredentials.Service}");
                    };

                    agent.CredentialsUpdated += (sender, e) =>
                    {
                        // Here you update your saved state. Saving the access token is optional.
                        //
                        // When your app restarts you can then use the RefreshCredentials(AtProtoCredential, CancellationToken) method on the Agent
                        // to try to restore the session you had.
                        persistedLoginState = new StoredAuthenticationState(
                            authenticationType: e.AccessCredentials.AuthenticationType,
                            did: e.AccessCredentials.Did,
                            service: e.AccessCredentials.Service,
                            accessToken: e.AccessCredentials.AccessJwt,
                            refreshToken: e.AccessCredentials.RefreshToken);

                        if (e.AccessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
                        {
                            persistedLoginState.DPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                            persistedLoginState.DPoPNonce = dPoPAccessCredentials.DPoPNonce;
                        }

                        Console.WriteLine($"EVENT: Credentials updated for : {e.AccessCredentials.Did}");
                    };

                    agent.Unauthenticated += (sender, e) =>
                    {
                        // Here you would clear any saved authentication state for the DID.
                        persistedLoginState = null;

                        Console.WriteLine($"EVENT: {e.Did} logged out from {e.Service}.");
                    };

                    agent.TokenRefreshFailed += (sender, e) =>
                    {
                        // Here you would clear any saved authentication state for the DID because the session
                        // could not refresh and so any tokens you saved will be invalid.
                        persistedLoginState = null;

                        Console.WriteLine($"EVENT: Token Refresh failed API returned {e.StatusCode}");

                        if (e.Error is not null)
                        {
                            Console.WriteLine($"       {e.Error.Error}");

                            if (!string.IsNullOrEmpty(e.Error.Message))
                            {
                                Console.WriteLine($"       {e.Error.Message}");
                            }
                        }
                    };

                    // First let's rebuild an access / refresh pair

                    var savedState = persistedLoginState;

                    AtProtoCredential restoredCredential = AtProtoCredential.Create(
                        service: persistedLoginState.Service,
                        authenticationType: persistedLoginState.AuthenticationType,
                        accessJwt: persistedLoginState.AccessToken,
                        refreshToken: persistedLoginState.RefreshToken,
                        dPoPProofKey: persistedLoginState.DPoPProofKey,
                        dPoPNonce: persistedLoginState.DPoPNonce);

                    if (!await agent.RefreshCredentials(restoredCredential, cancellationToken) || !agent.IsAuthenticated)
                    {
                        Console.WriteLine("❌\tRestore failed for access/refresh pair");
                    }

                    if (savedState.Equals(persistedLoginState))
                    {
                        Console.WriteLine("❌\tPersisted state did not change");
                        return;
                    }

                    // Now try just a refresh token
                    AtProtoCredential refreshOnlyCredential = AtProtoCredential.Create(
                        service: persistedLoginState.Service,
                        authenticationType: persistedLoginState.AuthenticationType,
                        refreshToken: persistedLoginState.RefreshToken);

                    if (!await agent.RefreshCredentials(restoredCredential, cancellationToken) || !agent.IsAuthenticated)
                    {
                        Console.WriteLine("❌\tRestore failed for refresh only");
                    }
                }

                Debugger.Break();

                // Now let's do OAuth

                using (var agent = new BlueskyAgent(
                    new BlueskyAgentOptions()
                    {
                        LoggerFactory = loggerFactory,

                        HttpClientOptions = new HttpClientOptions()
                        {
                            CheckCertificateRevocationList = checkCertificateRevocationList,
                            ProxyUri = proxyUri
                        },

                        OAuthOptions = new OAuthOptions()
                        {
                            ClientId = "http://localhost",
                            Scopes = ["atproto", "transition:generic"]
                        }
                    }))
                {
                    // same event code as before

                    agent.Authenticated += (sender, e) =>
                    {
                        // Here you would save the DID, refresh token and service to the appropriate secure
                        // storage location for your platform (Windows Credential store for Windows,
                        // Keyring for Apple platforms, and for Linux the user's profile directory and hope
                        // the permissions are correct.
                        //
                        // Saving the access token is optional, as is saving the authentication type.
                        //
                        // When your app restarts you can then use the RestoreSession() method on the Agent
                        // to try to restore the session you had.
                        //
                        // If you save the access token RestoreSession() will try to use that first, before
                        // falling back to using the refresh token to create a new authenticated session.
                        persistedLoginState = new StoredAuthenticationState(
                            authenticationType: e.AccessCredentials.AuthenticationType,
                            did: e.AccessCredentials.Did,
                            service: e.AccessCredentials.Service,
                            accessToken: e.AccessCredentials.AccessJwt,
                            refreshToken: e.AccessCredentials.RefreshToken);

                        if (e.AccessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
                        {
                            persistedLoginState.DPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                            persistedLoginState.DPoPNonce = dPoPAccessCredentials.DPoPNonce;
                        }

                        Console.WriteLine($"EVENT: {e.AccessCredentials.Did} authenticated on {e.AccessCredentials.Service}");
                    };

                    agent.CredentialsUpdated += (sender, e) =>
                    {
                        // Here you update your saved state. Saving the access token is optional.
                        //
                        // When your app restarts you can then use the RefreshCredentials(AtProtoCredential, CancellationToken) method on the Agent
                        // to try to restore the session you had.
                        persistedLoginState = new StoredAuthenticationState(
                            authenticationType: e.AccessCredentials.AuthenticationType,
                            did: e.AccessCredentials.Did,
                            service: e.AccessCredentials.Service,
                            accessToken: e.AccessCredentials.AccessJwt,
                            refreshToken: e.AccessCredentials.RefreshToken);

                        if (e.AccessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
                        {
                            persistedLoginState.DPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                            persistedLoginState.DPoPNonce = dPoPAccessCredentials.DPoPNonce;
                        }

                        Console.WriteLine($"EVENT: Credentials updated for : {e.AccessCredentials.Did}");
                    };

                    agent.Unauthenticated += (sender, e) =>
                    {
                        // Here you would clear any saved authentication state for the DID.
                        persistedLoginState = null;

                        Console.WriteLine($"EVENT: {e.Did} logged out from {e.Service}.");
                    };

                    agent.TokenRefreshFailed += (sender, e) =>
                    {
                        // Here you would clear any saved authentication state for the DID because the session
                        // could not refresh and so any tokens you saved will be invalid.
                        persistedLoginState = null;

                        Console.WriteLine($"EVENT: Token Refresh failed API returned {e.StatusCode}");

                        if (e.Error is not null)
                        {
                            Console.WriteLine($"       {e.Error.Error}");

                            if (!string.IsNullOrEmpty(e.Error.Message))
                            {
                                Console.WriteLine($"       {e.Error.Message}");
                            }
                        }
                    };

                    using (var callbackServer = new idunno.AtProto.OAuthCallback.CallbackServer(
                        idunno.AtProto.OAuthCallback.CallbackServer.GetRandomUnusedPort(),
                        loggerFactory: loggerFactory))
                    {
                        OAuthClient oAuthClient = agent.CreateOAuthClient();

                        Uri startUri = await agent.BuildOAuth2LoginUri(oAuthClient, handle, returnUri: callbackServer.Uri, cancellationToken: cancellationToken);

                        OAuthClient.OpenBrowser(startUri);

                        string callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (string.IsNullOrEmpty(callbackData))
                        {
                            ConsoleColor oldColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Received no login response");
                            Console.ForegroundColor = oldColor;
                            return;
                        }

                        Console.WriteLine("Logging in");

                        if (!await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken) || !agent.IsAuthenticated)
                        {
                            ConsoleColor oldColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error parsing oauth callback data");
                            Console.ForegroundColor = oldColor;
                            return;
                        }
                    }

                    Console.WriteLine("Refreshing credentials");

                    var refreshSessionResult = await agent.RefreshCredentials(cancellationToken: cancellationToken);

                    Console.WriteLine($"Refresh credentials result: {refreshSessionResult}");

                    Console.WriteLine("Logging out");

                    await agent.Logout(cancellationToken: cancellationToken);

                    Console.WriteLine("Refreshing the credentials once the agent has logged out.");

                    // We know this will throw an exception.
                    try
                    {
                        _ = await agent.RefreshCredentials(cancellationToken: cancellationToken);
                    }
                    catch (AuthenticationRequiredException e)
                    {
                        Console.WriteLine($"Exception Thrown : {e.Message}");
                    }

                    // Login again
                    Console.WriteLine("Logging in again");
                    using (var callbackServer = new idunno.AtProto.OAuthCallback.CallbackServer(
                        idunno.AtProto.OAuthCallback.CallbackServer.GetRandomUnusedPort(),
                        loggerFactory: loggerFactory))
                    {
                        OAuthClient oAuthClient = agent.CreateOAuthClient();

                        Uri startUri = await agent.BuildOAuth2LoginUri(oAuthClient, handle, returnUri: callbackServer.Uri, cancellationToken: cancellationToken);

                        OAuthClient.OpenBrowser(startUri);

                        string callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (string.IsNullOrEmpty(callbackData))
                        {
                            ConsoleColor oldColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Received no login response");
                            Console.ForegroundColor = oldColor;
                            return;
                        }

                        Console.WriteLine("Logging in");

                        if (!await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken) || !agent.IsAuthenticated)
                        {
                            ConsoleColor oldColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error parsing oauth callback data");
                            Console.ForegroundColor = oldColor;
                            return;
                        }
                    }

                    // Now mess with the refresh token to cause the refresh call to fail
                    if (agent.Credentials is not null)
                    {
                        // First let's copy the persisted token, because when the refresh fails it'll clear it.
                        StoredAuthenticationState? copiedLoginState = persistedLoginState;

                        agent.Credentials.RefreshToken = "invalid";

                        Console.WriteLine("Refreshing the credentials with an invalid refresh token.");
                        await agent.RefreshCredentials(cancellationToken: cancellationToken);

                        // And now restore that good persisted state and pretend the refresh fail never happened.
                        persistedLoginState = copiedLoginState;
                    }
                }

                // Now let's try to recreate credentials from the persisted state
                if (persistedLoginState is null)
                {
                    Console.WriteLine("❌\tNo persisted state to restore");
                    return;
                }

                if (persistedLoginState.AuthenticationType != AuthenticationType.OAuth)
                {
                    Console.WriteLine("❌\tPersisted state AuthenticationType is not OAuth");
                    return;
                }

                if (persistedLoginState.DPoPNonce is null || persistedLoginState.DPoPProofKey is null)
                {
                    Console.WriteLine("\tNo DPoP state to restore");
                    return;
                }

                Console.Write("Attempting to restore session...");

                using (var agent = new BlueskyAgent(
                    options: new BlueskyAgentOptions
                    {
                        LoggerFactory = loggerFactory,
                        HttpClientOptions = new HttpClientOptions()
                        {
                            ProxyUri = proxyUri,
                            CheckCertificateRevocationList = checkCertificateRevocationList
                        },

                        OAuthOptions = new OAuthOptions()
                        {
                            ClientId = "http://localhost",
                            Scopes = ["atproto", "transition:generic"]
                        }
                    }))
                {
                    agent.Authenticated += (sender, e) =>
                    {
                        // Here you would save the DID, refresh token and service to the appropriate secure
                        // storage location for your platform (Windows Credential store for Windows,
                        // Keyring for Apple platforms, and for Linux the user's profile directory and hope
                        // the permissions are correct.
                        //
                        // Saving the access token is optional, as is saving the authentication type.
                        //
                        // When your app restarts you can then use the RestoreSession() method on the Agent
                        // to try to restore the session you had.
                        //
                        // If you save the access token RestoreSession() will try to use that first, before
                        // falling back to using the refresh token to create a new authenticated session.
                        persistedLoginState = new StoredAuthenticationState(
                            authenticationType: e.AccessCredentials.AuthenticationType,
                            did: e.AccessCredentials.Did,
                            service: e.AccessCredentials.Service,
                            accessToken: e.AccessCredentials.AccessJwt,
                            refreshToken: e.AccessCredentials.RefreshToken);

                        if (e.AccessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
                        {
                            persistedLoginState.DPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                            persistedLoginState.DPoPNonce = dPoPAccessCredentials.DPoPNonce;
                        }

                        Console.WriteLine($"EVENT: {e.AccessCredentials.Did} authenticated on {e.AccessCredentials.Service}");
                    };

                    agent.CredentialsUpdated += (sender, e) =>
                    {
                        // Here you update your saved state. Saving the access token is optional.
                        //
                        // When your app restarts you can then use the RefreshCredentials(AtProtoCredential, CancellationToken) method on the Agent
                        // to try to restore the session you had.
                        persistedLoginState = new StoredAuthenticationState(
                            authenticationType: e.AccessCredentials.AuthenticationType,
                            did: e.AccessCredentials.Did,
                            service: e.AccessCredentials.Service,
                            accessToken: e.AccessCredentials.AccessJwt,
                            refreshToken: e.AccessCredentials.RefreshToken);

                        if (e.AccessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
                        {
                            persistedLoginState.DPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                            persistedLoginState.DPoPNonce = dPoPAccessCredentials.DPoPNonce;
                        }

                        Console.WriteLine($"EVENT: Credentials updated for : {e.AccessCredentials.Did}");
                    };

                    agent.Unauthenticated += (sender, e) =>
                    {
                        // Here you would clear any saved authentication state for the DID.
                        persistedLoginState = null;

                        Console.WriteLine($"EVENT: {e.Did} logged out from {e.Service}.");
                    };

                    agent.TokenRefreshFailed += (sender, e) =>
                    {
                        // Here you would clear any saved authentication state for the DID because the session
                        // could not refresh and so any tokens you saved will be invalid.
                        persistedLoginState = null;

                        Console.WriteLine($"EVENT: Token Refresh failed API returned {e.StatusCode}");

                        if (e.Error is not null)
                        {
                            Console.WriteLine($"       {e.Error.Error}");

                            if (!string.IsNullOrEmpty(e.Error.Message))
                            {
                                Console.WriteLine($"       {e.Error.Message}");
                            }
                        }
                    };

                    // First let's rebuild an access / refresh pair

                    StoredAuthenticationState savedState = persistedLoginState;

                    AtProtoCredential restoredCredential = AtProtoCredential.Create(
                        service: persistedLoginState.Service,
                        authenticationType: persistedLoginState.AuthenticationType,
                        accessJwt: persistedLoginState.AccessToken,
                        refreshToken: persistedLoginState.RefreshToken,
                        dPoPProofKey: persistedLoginState.DPoPProofKey,
                        dPoPNonce: persistedLoginState.DPoPNonce);

                    if (!await agent.RefreshCredentials(restoredCredential, cancellationToken) || !agent.IsAuthenticated)
                    {
                        Console.WriteLine("❌\tRestore failed for access/refresh pair");
                        return;
                    }

                    Console.WriteLine("✔\tRestore succeeded for access/refresh pair");

                    if (persistedLoginState.Equals(savedState))
                    {
                        Console.WriteLine("❌\tPersisted state did not change.");
                        return;
                    }

                    AtProtoCredential refreshOnlyCredential = AtProtoCredential.Create(
                        service: persistedLoginState.Service,
                        authenticationType: persistedLoginState.AuthenticationType,
                        refreshToken: persistedLoginState.RefreshToken,
                        dPoPProofKey: persistedLoginState.DPoPProofKey,
                        dPoPNonce: persistedLoginState.DPoPNonce);

                    if (!await agent.RefreshCredentials(refreshOnlyCredential, cancellationToken) || !agent.IsAuthenticated)
                    {
                        Console.WriteLine("❌\tRestore failed for refresh only");
                        return;
                    }

                    Console.WriteLine("✔\tRestore succeeded for refresh only");
                }

            }

                return;
        }
    }

    /// <summary>
    /// An in-memory store for session information.
    /// </summary>
    sealed record StoredAuthenticationState
    {
        public StoredAuthenticationState(Did did, Uri service, AuthenticationType authenticationType, string accessToken, string refreshToken, string? dPoPProofKey = null, string? dPoPNonce = null)
        {
            Did = did;
            AccessToken = accessToken;
            AuthenticationType = authenticationType;
            RefreshToken = refreshToken;
            Service = service;
            DPoPProofKey = dPoPProofKey;
            DPoPNonce = dPoPNonce;
        }

        /// <summary>
        /// The <see cref="Did"/> the tokens belong to.
        /// </summary>
        public Did Did { get; set; }

        /// <summary>
        /// The access token.
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// The type of authentication 
        /// </summary>
        public AuthenticationType AuthenticationType { get; set; }

        /// <summary>
        /// A refresh token that is used to update the access token.
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// The <see cref="Uri"/> of the service that issued the tokens.
        /// </summary>
        public Uri Service { get; set; }

        /// <summary>
        /// The signing key to use when making API requests.
        /// </summary>
        /// <remarks>
        ///<para>This is only applicable if OAuth authentication was used to create the <see cref="AccessToken"/> and <see cref="RefreshToken"/>.</para>
        /// </remarks>
        public string? DPoPProofKey { get; set; }

        /// <summary>
        /// The nonce to use when making signed API requests.
        /// </summary>
        /// <remarks>
        ///<para>This is only applicable if OAuth authentication was used to create the <see cref="AccessToken"/> and <see cref="RefreshToken"/>.</para>
        /// </remarks>
        public string? DPoPNonce { get; set; }
    }
}
