// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication.Models;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates a session on a PDS created by a handle/password login.
    /// </summary>
    public record Session
    {
        internal Session(CreateSessionResponse createSessionResponse)
        {
            ArgumentNullException.ThrowIfNull(createSessionResponse);

            Handle = createSessionResponse.Handle;
            Did = createSessionResponse.Did;
            DidDoc = createSessionResponse.DidDoc;
            Active = createSessionResponse.Active;

            // Attempting to avoid the trimming errors with enum use
            // See https://github.com/dotnet/runtime/issues/114307
            if (createSessionResponse.Status is not null)
            {
                switch (createSessionResponse.Status.ToUpperInvariant())
                {
                    case "TAKENDOWN":
                        Status = AccountStatus.Takendown;
                        break;

                    case "SUSPENDED":
                        Status = AccountStatus.Suspended;
                        break;

                    case "DEACTIVATED":
                        Status = AccountStatus.Deactivated;
                        break;

                    default:
                        break;
                }
            }

            AccessJwt = createSessionResponse.AccessJwt;
            RefreshJwt = createSessionResponse.RefreshJwt;
            Email = createSessionResponse.Email;
            EmailConfirmed = createSessionResponse.EmailConfirmed;
            EmailAuthFactor = createSessionResponse.EmailAuthFactor;
        }

        internal Session(GetSessionResponse getSessionResponse, AccessCredentials accessCredentials) 
        {
            ArgumentNullException.ThrowIfNull(getSessionResponse);

            Handle = getSessionResponse.Handle;
            Did = getSessionResponse.Did;
            DidDoc = getSessionResponse.DidDoc;
            Active = getSessionResponse.Active;

            if (getSessionResponse.Status is not null)
            {
                switch (getSessionResponse.Status.ToUpperInvariant())
                {
                    case "TAKENDOWN":
                        Status = AccountStatus.Takendown;
                        break;

                    case "SUSPENDED":
                        Status = AccountStatus.Suspended;
                        break;

                    case "DEACTIVATED":
                        Status = AccountStatus.Deactivated;
                        break;

                    default:
                        break;
                }
            }

            AccessJwt = accessCredentials.AccessJwt;
            RefreshJwt = accessCredentials.RefreshToken;
            Email = getSessionResponse.Email;
            EmailConfirmed = getSessionResponse.EmailConfirmed;
            EmailAuthFactor = getSessionResponse.EmailAuthFactor;
        }

        /// <summary>
        /// The <see cref="AtProto.Handle">Handle</see> the session belongs to.
        /// </summary>
        public Handle Handle { get; init; }

        /// <summary>
        /// The <see cref="AtProto.Did">Did</see> the newly created session belongs to.
        /// </summary>
        public Did Did { get; init; }

        /// <summary>
        /// The <see cref="DidDocument">DidDocument</see> for the <see cref="Did"/> that the session belongs to.
        /// </summary>
        public DidDocument? DidDoc { get; init; }

        /// <summary>
        /// A flag indicating whether the account associated with the session is active.
        /// </summary>
        public bool? Active { get; init; }

        /// <summary>
        /// If <see cref="Active"/> is <see langword="false"/>, a possible reason the account is inactive.
        /// </summary>
        /// <remarks>
        /// <para>If <see cref="Active"/> and no status is supplied, then the host makes no claim for why the repository is no longer being hosted.</para>
        /// </remarks>
        public AccountStatus? Status { get; init; }

        /// <summary>
        /// The Access JWT for the session.
        /// </summary>
        public string AccessJwt { get; init; }

        /// <summary>
        /// The Refresh JWT for the session.
        /// </summary>
        public string RefreshJwt { get; init; }

        /// <summary>
        /// The email associated with <see cref="Handle">Handle</see> the session belongs to.
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// A flag indicating whether the <see cref="Email"/> is confirmed or not.
        /// </summary>
        public bool? EmailConfirmed { get; init; }

        /// <summary>
        /// A flag indicating whether the session required an email based authentication token.
        /// </summary>
        public bool? EmailAuthFactor { get; init; }
    }
}
