// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Loads confidential client signing keys from <see cref="OAuthOptions.ClientSigningKeyPath"/> and
/// <see cref="OAuthOptions.AdditionalClientSigningKeyPaths"/> into <see cref="OAuthOptions.ClientSigningKey"/> and
/// <see cref="OAuthOptions.AdditionalClientSigningKeys"/>.
/// </summary>
internal sealed class ClientSigningKeyPostConfigureOptions(IHostEnvironment? hostEnvironment) : IPostConfigureOptions<BlueskyAgentOptions>
{
    private readonly ConcurrentDictionary<(string Path, string? KeyId), Lazy<OAuthClientSigningKey>> _signingKeys = new();

    public void PostConfigure(string? name, BlueskyAgentOptions options)
    {
        if (options?.OAuthOptions is not OAuthOptions oAuthOptions)
        {
            return;
        }

        if (oAuthOptions.ClientSigningKey is null && !string.IsNullOrWhiteSpace(oAuthOptions.ClientSigningKeyPath))
        {
            oAuthOptions.ClientSigningKey = LoadKey(oAuthOptions.ClientSigningKeyPath, oAuthOptions.ClientSigningKeyId);
        }

        foreach (string path in oAuthOptions.AdditionalClientSigningKeyPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            OAuthClientSigningKey key = LoadKey(path, null);

            if (!oAuthOptions.AdditionalClientSigningKeys.Any(existing => existing is not null && string.Equals(existing.KeyId, key.KeyId, StringComparison.Ordinal)))
            {
                oAuthOptions.AdditionalClientSigningKeys.Add(key);
            }
        }
    }

    private OAuthClientSigningKey LoadKey(string path, string? keyId) =>
        _signingKeys.GetOrAdd(
            (ResolvePath(path, hostEnvironment?.ContentRootPath), keyId),
            static key => new Lazy<OAuthClientSigningKey>(
                () => OAuthClientSigningKey.FromPemFile(key.Path, key.KeyId),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>
    /// Expands environment variables and a leading <c>~</c> in <paramref name="path"/>, then resolves a relative path against
    /// <paramref name="contentRootPath"/>.
    /// </summary>
    /// <param name="path">The configured path.</param>
    /// <param name="contentRootPath">The application content root, or <see langword="null"/> to use the current directory.</param>
    /// <returns>The full path of the key file.</returns>
    internal static string ResolvePath(string path, string? contentRootPath)
    {
        string expanded = Environment.ExpandEnvironmentVariables(path.Trim());

        if (expanded == "~" || expanded.StartsWith("~/", StringComparison.Ordinal) || expanded.StartsWith("~\\", StringComparison.Ordinal))
        {
            expanded = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded[1..].TrimStart('/', '\\'));
        }

        return string.IsNullOrEmpty(contentRootPath) ?
            Path.GetFullPath(expanded) :
            Path.GetFullPath(expanded, contentRootPath);
    }
}