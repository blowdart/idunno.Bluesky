// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto
{
    /// <summary>
    /// Encapsulates the information needed to make authenticated ATProto requests.
    /// </summary>
    public sealed record TokenState
    {
        /// <summary>
        /// Creates a new instance of <see cref="TokenState"/>.
        /// </summary>
        /// <param name="accessToken">The access token needed to make an authenticated request.</param>
        /// <param name="refreshToken">The refresh token needed to acquire a new access token when it has expired.</param>
        /// <param name="dPoPKey">The optional DPoP key used to sign requests if the <see cref="AccessToken"/> was created from an OAuth login.</param>
        /// <param name="dPoPNonce">The optional nonce used in signing requests if the <see cref="AccessToken"/> was created from an OAuth login.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="accessToken"/> or <paramref name="refreshToken"/> is null or empty, or when
        /// <paramref name="dPoPKey"/> is not null and <paramref name="dPoPNonce"/> is null or empty, or when
        /// <paramref name="dPoPNonce"/> is not null and <paramref name="dPoPKey"/> is null or empty.
        /// </exception>
        public TokenState(string accessToken, string refreshToken, string? dPoPKey = null, string? dPoPNonce = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(accessToken);
            ArgumentException.ThrowIfNullOrEmpty(refreshToken);

            if (dPoPKey is not null)
            {
                ArgumentException.ThrowIfNullOrEmpty(dPoPKey);
                ArgumentException.ThrowIfNullOrEmpty(dPoPNonce);
            }

            if (dPoPNonce is not null)
            {
                ArgumentException.ThrowIfNullOrEmpty(dPoPKey);
                ArgumentException.ThrowIfNullOrEmpty(dPoPNonce);
            }

            AccessToken = accessToken;
            RefreshToken = refreshToken;

            DPoPKey = dPoPKey;
            DPoPNonce = dPoPNonce;
        }

        /// <summary>
        /// Gets the access token needed to make an authenticated request.
        /// </summary>
        public string AccessToken { get; internal set; }

        /// <summary>
        /// Gets the refresh token needed to acquire a new access token when it has expired.
        /// </summary>
        public string RefreshToken { get; internal set; }

        /// <summary>
        /// Gets the optional DPoP key used to sign requests if the <see cref="AccessToken"/> was created from an OAuth login.
        /// </summary>
        public string? DPoPKey { get; internal set; }

        /// <summary>
        /// Gets the optional nonce used in signing requests if the <see cref="AccessToken"/> was created from an OAuth login.
        /// </summary>
        public string? DPoPNonce { get; internal set; }

        /// <summary>
        /// Flag indicating that DPoP is enabled for the <see cref="AccessToken"/> and requests should be signed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(DPoPKey))]
        [MemberNotNullWhen(true, nameof(DPoPNonce))]
        public bool DPoPEnabled => DPoPKey is not null && DPoPNonce is not null;
    }
}
