// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

namespace idunno.Bluesky.BotSdk;

// Read once at startup. A restart applies changed policy to future ingestion and sending,
// but does not rewrite reply records already chosen and persisted in the outbox.
/// <summary>Configures durable replay, reply delivery, authentication, and conservative rate limits.</summary>
/// <remarks><para>Read options once at startup. Changes must not rewrite records already saved for delivery.</para></remarks>
public class BotOptions
{
    /// <summary>The default configuration section name for bot options.</summary>
    public const string Bot = "Bot";

    // Default to no posting so trying the sample cannot accidentally publish replies.
    /// <summary>Gets or sets a value that indicates whether replies are simulated without posting. The default is <see langword="true"/>.</summary>
    public bool DeveloperMode { get; set; } = true;
    /// <summary>Gets or sets the bot account's DID.</summary>
    public string Did { get; set; } = "";
    /// <summary>Gets or sets the bot account's login handle.</summary>
    public string Handle { get; set; } = "";
    /// <summary>Gets or sets the bot account's app password.</summary>
    public string AppPassword { get; set; } = "";

    // Archive access and account login are separate credentials with different purposes.
    /// <summary>Gets or sets the archive API key, separate from the account password.</summary>
    public string ArchiveApiKey { get; set; } = "";
    /// <summary>Gets or sets the base64-encoded 32-byte credential encryption key.</summary>
    public string CredentialEncryptionKey { get; set; } = "";
    /// <summary>Gets or sets the Jetstream v2 service. Saved checkpoints remain bound to this service.</summary>
    public string JetstreamService { get; set; } = "wss://jetstream.us-west.bsky.network";
    /// <summary>Gets or sets the live replay buffer capacity. The default is 8192 events.</summary>
    public int ReplayLiveBufferCapacity { get; set; } = 8192;
    // Stay below published Bluesky-hosted limits. These budgets cover this bot only, not other
    // applications sharing its account/IP; actual server cooldowns may require longer waits.
    /// <summary>Gets or sets the minimum outbound request interval in seconds. The default is 5.</summary>
    public int MinimumRequestIntervalSeconds { get; set; } = 5;
    /// <summary>Gets or sets the hourly write-point budget. The default is 4500.</summary>
    public int HourlyWritePoints { get; set; } = 4500;
    /// <summary>Gets or sets the daily write-point budget. The default is 30000.</summary>
    public int DailyWritePoints { get; set; } = 30000;
    /// <summary>Gets or sets the five-minute request budget. The default is 2400.</summary>
    public int RequestsPerFiveMinutes { get; set; } = 2400;
    /// <summary>Gets or sets the five-minute session-creation budget. The default is 20.</summary>
    public int SessionsPerFiveMinutes { get; set; } = 20;
    /// <summary>Gets or sets the daily session-creation budget. The default is 200.</summary>
    public int SessionsPerDay { get; set; } = 200;
    /// <summary>Gets or sets completed reply retention in days. The default is 7.</summary>
    public int CompletedRetentionDays { get; set; } = 7;

    /// <summary>Validates identities, secrets, replay configuration, rate limits, and retention.</summary>
    /// <exception cref="InvalidOperationException">The configuration is invalid or unsafe.</exception>
    /// <exception cref="ArgumentException">A required secret is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The live buffer capacity is not positive.</exception>
    public virtual void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ReplayLiveBufferCapacity);

        if (!idunno.AtProto.Did.TryParse(Did, out _))
        {
            throw new InvalidOperationException("Bot:Did must be the bot account's DID, including in developer mode.");
        }

        if (!Uri.TryCreate(JetstreamService, UriKind.Absolute, out Uri? service) ||
            service.Scheme != "wss" || service.AbsolutePath != "/" || !string.IsNullOrEmpty(service.Query) ||
            !string.IsNullOrEmpty(service.Fragment) || !string.IsNullOrEmpty(service.UserInfo))
        {
            throw new InvalidOperationException("Bot:JetstreamService must be a wss:// Jetstream v2 host without a path, query, or credentials.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ArchiveApiKey);

        if (!DeveloperMode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Handle);
            ArgumentException.ThrowIfNullOrWhiteSpace(AppPassword);

            if (!idunno.AtProto.Handle.TryParse(Handle, out _))
            {
                throw new InvalidOperationException("Bot:Handle must be a valid Bluesky handle.");
            }
        }

        if (MinimumRequestIntervalSeconds is < 5 or > 3600 ||
            HourlyWritePoints is < 3 or > 4500 || DailyWritePoints is < 3 or > 30000 ||
            RequestsPerFiveMinutes is < 1 or > 2400 || SessionsPerFiveMinutes is < 1 or > 20 ||
            SessionsPerDay is < 1 or > 200 || CompletedRetentionDays is < 1 or > 365)
        {
            throw new InvalidOperationException("Rate limits or retention are outside the supported conservative bounds.");
        }

        if (!DeveloperMode)
        {
            byte[] key;

            try
            {
                key = Convert.FromBase64String(CredentialEncryptionKey);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Bot:CredentialEncryptionKey must be a base64-encoded 32-byte key.", exception);
            }

            if (key.Length != 32)
            {
                throw new InvalidOperationException("Bot:CredentialEncryptionKey must be a base64-encoded 32-byte key.");
            }

            CryptographicOperations.ZeroMemory(key);
        }
    }

}
