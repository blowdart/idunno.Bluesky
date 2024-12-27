// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.IdentityModel.JsonWebTokens;

using idunno.AtProto.Models;

namespace idunno.AtProto.Server
{
    /// <summary>
    /// Representation information about, and the state of a session on an AT Proto service.
    /// </summary>
    public sealed record Session
    {
        private readonly object _syncRoot = new ();

        private string? _accessJwt;

        internal Session(Uri service, CreateSessionResponse createSessionResult)
        {
            Service = service;

            AccessJwt = createSessionResult.AccessJwt;
            RefreshJwt = createSessionResult.RefreshJwt;
            Handle = createSessionResult.Handle;
            Did = createSessionResult.Did;

            DidDoc = createSessionResult.DidDoc;
            Email = createSessionResult.Email;
            EmailConfirmed = createSessionResult.EmailConfirmed;
            EmailAuthFactor = createSessionResult.EmailAuthFactor;
            IsAccountActive = createSessionResult.Active;
            AccountStatus = createSessionResult.Status;
        }

        internal Session(Uri service, GetSessionResponse getSessionResponse)
        {
            Service = service;

            Did = getSessionResponse.Did;
            Handle = getSessionResponse.Handle;

            Email = getSessionResponse.Email;
            EmailConfirmed = getSessionResponse.EmailConfirmed;
            EmailAuthFactor = getSessionResponse.EmailAuthFactor;
            DidDoc = getSessionResponse.DidDoc;
            IsAccountActive = getSessionResponse.Active;
            AccountStatus = getSessionResponse.Status;
        }

        internal Session(Uri service, GetSessionResponse getSessionResponse, string? accessToken, string? refreshToken) : this(service, getSessionResponse)
        {
            UpdateAccessTokens(accessToken, refreshToken);
        }

        /// <summary>
        /// Gets the access token for the actor whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>The access token is attached automatically to every API call through an agent that requires authentication.</para>
        /// </remarks>
        public string? AccessJwt
        {
            get
            {
                return _accessJwt;
            }

            private set
            {
                _accessJwt = value;
                AccessJwtExpiresOn = GetJwtExpiry(value);
            }
        }

        /// <summary>
        /// Gets the <see cref="DateTime"/> the access token expires on, if an access token is present.
        /// </summary>
        public DateTime? AccessJwtExpiresOn { get; private set; }

        /// <summary>
        /// Gets the refresh token for the actor whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>The refresh token is used to exchange an expiring access token for a new access token.</para>
        /// </remarks>
        public string? RefreshJwt { get; private set; }

        /// <summary>
        /// Gets the <see cref="Did"/> of the actor whose authentication produced this Session instance.
        /// </summary>
        public Did Did { get; init; }

        /// <summary>
        /// Gets the <see cref="Handle"/> of the actor whose authentication produced this Session instance.
        /// </summary>
        public Handle Handle { get; init; }

        /// <summary>
        /// Gets the <see cref="DidDoc " /> of the actor whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>A DID document actus as a profile for its subject, containing metadata about the subject such
        /// as signing keys and service endpoints to where the subject's data is stored.</para>
        /// </remarks>
        public DidDocument? DidDoc { get; init; }

        /// <summary>
        /// Gets the email of the actor whose authentication produced this Session instance.
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// Gets a flag indicating if the <see cref="Email"/> has been confirmed.
        /// </summary>
        public bool? EmailConfirmed { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the actor used an email authentication factor during login.
        /// </summary>
        public bool? EmailAuthFactor { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the actor's account is active or not.
        /// </summary>
        public bool? IsAccountActive { get; init; }

        /// <summary>
        /// Indicates a possible reason for why the actor's account is not active.
        /// If <see cref="IsAccountActive"/> is false and no status is supplied, then the host makes no claim
        /// for why the repository is no longer being hosted.
        /// </summary>
        public AccountStatus? AccountStatus { get; init; } 

        /// <summary>
        /// Gets the URI of the service that the <see cref="Session"/> instance was created on.
        /// </summary>
        public Uri? Service { get; init; }

        /// <summary>
        /// Returns a flag indicating whether this session has an access token.
        /// </summary>
        [MemberNotNullWhen(true, nameof(AccessJwt))]
        [MemberNotNullWhen(true, nameof(AccessJwtExpiresOn))]
        public bool HasAccessToken
        {
            get
            {
                return !string.IsNullOrWhiteSpace(AccessJwt);
            }
        }

        internal void UpdateAccessTokens(string? accessJwt, string? refreshJwt)
        {
            lock (_syncRoot)
            {
                AccessJwt = accessJwt;
                RefreshJwt = refreshJwt;
            }
        }

        private static DateTime? GetJwtExpiry(string? jwt)
        {
            if (string.IsNullOrEmpty(jwt))
            {
                return null;
            }

            JsonWebToken token = new(jwt);
            return token.ValidTo;
        }
    }
}
