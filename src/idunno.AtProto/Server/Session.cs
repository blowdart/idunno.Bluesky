// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Server
{
    /// <summary>
    /// Representation information about, and the state of a session on an AT Proto service.
    /// </summary>
    public sealed record Session
    {
        internal Session(Uri service, CreateSessionResponse createSessionResult)
        {
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

            Service = service;
        }

        internal Session(Uri service, GetSessionResponse getSessionResult)
        {
            Did = getSessionResult.Did;
            Handle = getSessionResult.Handle;

            Email = getSessionResult.Email;
            EmailConfirmed = getSessionResult.EmailConfirmed;
            EmailAuthFactor = getSessionResult.EmailAuthFactor;
            DidDoc = getSessionResult.DidDoc;
            IsAccountActive = getSessionResult.Active;
            AccountStatus = getSessionResult.Status;

            Service = service;
        }

        /// <summary>
        /// Gets the access token for the user whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>The access token is attached automatically to every Bluesky API call that requires authentication.</para>
        /// </remarks>
        public string? AccessJwt { get; internal set; }

        /// <summary>
        /// Gets the refresh token for the user whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>The refresh token is used to exchange an expiring access token for a new access token.</para>
        /// </remarks>
        public string? RefreshJwt { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Did"/> of the user whose authentication produced this Session instance.
        /// </summary>
        public Did Did { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Handle"/> of the user whose authentication produced this Session instance.
        /// </summary>
        public Handle Handle { get; internal set; }

        /// <summary>
        /// Gets the <see cref="DidDoc " /> of the user whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>A DID document actus as a profile for its subject, containing metadata about the subject such
        /// as signing keys and service endpoints to where the subject's data is stored.</para>
        /// </remarks>
        public DidDocument? DidDoc { get; internal set; }

        /// <summary>
        /// Gets the email of the user whose authentication produced this Session instance.
        /// </summary>
        public string? Email { get; internal set; }

        /// <summary>
        /// Gets a flag indicating if the <see cref="Email"/> has been confirmed.
        /// </summary>
        public bool? EmailConfirmed { get; internal set; }

        /// <summary>
        /// Gets a flag indicating whether the user used an email authentication factor during login.
        /// </summary>
        public bool? EmailAuthFactor { get; internal set; }

        /// <summary>
        /// Gets a flag indicating whether the account is active or not.
        /// </summary>
        public bool? IsAccountActive { get; internal set; }

        /// <summary>
        /// Indicates a possible reason for why the account is not active.
        /// If <see cref="IsAccountActive"/> is false and no status is supplied, then the host makes no claim
        /// for why the repository is no longer being hosted.
        /// </summary>
        public AccountStatus? AccountStatus { get; internal set; } 

        /// <summary>
        /// Gets the URI of the service that the <see cref="Session"/> instance was created on.
        /// </summary>
        public Uri? Service { get; internal set; }

        /// <summary>
        /// Gets the URI of the Personal Data Server (PDS) of the user whose authentication produced this Session instance.
        /// </summary>
        public Uri? PersonalDataServer
        {
            get
            {
                if (DidDoc is null ||
                    DidDoc.Services is null ||
                    !DidDoc.Services.Where(s => s.Id == @"#atproto_pds").Any())
                {
                    return null;
                }

                return DidDoc.Services.Where(s => s.Id == @"#atproto_pds").First().ServiceEndpoint;
            }
        }
    }
}
