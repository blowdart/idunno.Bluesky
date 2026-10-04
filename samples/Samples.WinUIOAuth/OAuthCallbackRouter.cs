// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto.Authentication;

namespace Samples.WinUIOAuth;

internal sealed class OAuthCallbackRouter(TimeProvider? timeProvider = null)
{
    internal const string ClientId = "https://bluesky.idunno.dev/windows-oauth-client.json";
    internal const string RedirectUri = "dev.idunno.bluesky:/callback";
    internal const string ProfileScope = "rpc:app.bsky.actor.getProfile?aud=did:web:api.bsky.app%23bsky_appview";
    internal static readonly TimeSpan LoginLifetime = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private OAuthLoginState? _pending;
    private DateTimeOffset _expires;

    internal void Begin(OAuthLoginState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_pending is not null)
        {
            throw new InvalidOperationException("A login is already pending.");
        }

        if (!string.Equals(state.RedirectUri, RedirectUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The login redirect URI does not match the published metadata.");
        }

        _pending = state;
        _expires = _timeProvider.GetUtcNow() + LoginLifetime;
    }

    internal void Clear()
    {
        _pending = null;
    }

    // Called only on the UI dispatcher. Consuming state before starting any asynchronous exchange
    // ensures that a second activation cannot redeem the same code.
    internal OAuthLoginState Take(Uri callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (_pending is null)
        {
            throw new InvalidOperationException("No login is pending. Start login in this window; callbacks cannot restore a closed session.");
        }

        if (_timeProvider.GetUtcNow() >= _expires)
        {
            Clear();
            throw new InvalidOperationException("The login has expired. Start login again.");
        }

        string text = callback.OriginalString;
        int queryStart = text.IndexOf('?');
        if (!callback.IsAbsoluteUri || text.Length > 16384 || queryStart < 0 ||
            !text.AsSpan(0, queryStart).SequenceEqual(RedirectUri) || text.Contains('#', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The callback address is invalid.");
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string parameter in text[(queryStart + 1)..].Split('&'))
        {
            int separator = parameter.IndexOf('=');
            if (separator < 1 || values.Count >= 32)
            {
                throw new InvalidOperationException("The callback query is malformed.");
            }

            string name = Decode(parameter[..separator]);
            string value = Decode(parameter[(separator + 1)..]);
            if (!values.TryAdd(name, value))
            {
                throw new InvalidOperationException("The callback contains duplicate parameters.");
            }
        }

        if (!values.TryGetValue("state", out string? state) ||
            !string.Equals(state, _pending.State, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The callback does not match the pending login.");
        }

        if (!values.TryGetValue("iss", out string? issuer) ||
            !Uri.TryCreate(issuer, UriKind.Absolute, out Uri? issuerUri) ||
            !string.Equals(issuerUri.AbsoluteUri, new Uri(_pending.ExpectedAuthority).AbsoluteUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The callback issuer does not match the pending login.");
        }

        bool hasCode = values.TryGetValue("code", out string? code);
        bool hasError = values.TryGetValue("error", out string? error);
        if (hasCode == hasError || (hasCode && string.IsNullOrWhiteSpace(code)) ||
            (hasError && string.IsNullOrWhiteSpace(error)))
        {
            throw new InvalidOperationException("The callback must contain either an authorization code or an OAuth error.");
        }

        OAuthLoginState result = _pending;
        Clear();
        return result;
    }

    private static string Decode(string value)
    {
        List<byte> bytes = [];
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '%')
            {
                if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2]))
                {
                    throw new InvalidOperationException("The callback contains invalid percent encoding.");
                }

                bytes.Add(Convert.ToByte(value.Substring(i + 1, 2), 16));
                i += 2;
            }
            else if (value[i] <= ' ' || value[i] > '~')
            {
                throw new InvalidOperationException("The callback contains an unescaped query character.");
            }
            else
            {
                bytes.Add(value[i] == '+' ? (byte)' ' : (byte)value[i]);
            }
        }

        string decoded;
        try
        {
            decoded = new UTF8Encoding(false, true).GetString([.. bytes]);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidOperationException("The callback contains invalid UTF-8.");
        }

        if (decoded.Any(char.IsControl))
        {
            throw new InvalidOperationException("The callback contains control characters.");
        }

        return decoded;
    }
}
