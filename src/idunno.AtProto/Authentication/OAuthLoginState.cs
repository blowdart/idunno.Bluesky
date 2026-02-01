// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Duende.IdentityModel.OidcClient;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates state that is generated when a start URI is created and must be present for the response to be parsed.
    /// </summary>
    public sealed class OAuthLoginState : IEquatable<OAuthLoginState>
    {
        /// <summary>
        /// Creates a new instance of <see cref="OAuthLoginState"/>.
        /// </summary>
        /// <param name="state">The state the needs to be hold between starting the authorize request and the response.</param>
        /// <param name="expectedAuthority">The expected authority that the access token should be issued for.</param>
        /// <param name="expectedService">The expected service that the access token should be issued for.</param>
        /// <param name="proofKey">The DPoP proof key which was used to sign the token request.</param>
        /// <param name="correlationId">The correlation identifier used in logging to tie requests and responses together.</param>
        /// <param name="extraProperties">Any extra properties to save in state.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        public OAuthLoginState(
            AuthorizeState state,
            string expectedAuthority,
            string expectedService,
            string proofKey,
            Guid correlationId,
            IDictionary<string, string>? extraProperties = null)
        {
            ArgumentNullException.ThrowIfNull(state);

            StartUrl = state.StartUrl;
            State = state.State;
            CodeVerifier = state.CodeVerifier;
            RedirectUri = state.RedirectUri;

            Error = state.Error;
            ErrorDescription = state.ErrorDescription;

            ExpectedAuthority = expectedAuthority;
            ExpectedService = expectedService;
            ProofKey = proofKey;
            CorrelationId = correlationId;
            ExtraProperties = extraProperties;
        }


        /// <summary>
        /// Creates a new instance of <see cref="OAuthLoginState"/>.
        /// </summary>
        /// <param name="startUrl">The start URL for the authorization flow, if any.</param>
        /// <param name="state">The state for the authorization flow, if any.</param>
        /// <param name="codeVerifier">The code verifier for the authorization flow, if any.</param>
        /// <param name="redirectUri">The redirect URI for the authorization flow, if any.</param>
        /// <param name="error">The error state.</param>
        /// <param name="errorDescription">The error state description.</param>
        /// <param name="expectedAuthority">The expected authority that the access token should be issued for.</param>
        /// <param name="expectedService">The expected service that the access token should be issued for.</param>
        /// <param name="proofKey">The DPoP proof key which was used to sign the token request.</param>
        /// <param name="correlationId">The correlation identifier used in logging to tie requests and responses together.</param>
        /// <param name="extraProperties">Any extra properties to save in state.</param>
        [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Matching the Duende property")]
        [JsonConstructor]
        public OAuthLoginState(
            string startUrl,
            string state,
            string codeVerifier,
            string redirectUri,
            string error,
            string errorDescription,
            string expectedAuthority,
            string expectedService,
            string proofKey,
            Guid correlationId,
            IDictionary<string, string>? extraProperties = null)
        {
            StartUrl = startUrl;
            State = state;
            CodeVerifier = codeVerifier;
            RedirectUri = redirectUri;

            Error = error;
            ErrorDescription = errorDescription;

            ExpectedAuthority = expectedAuthority;
            ExpectedService = expectedService;
            ProofKey = proofKey;
            CorrelationId = correlationId;
            ExtraProperties = extraProperties;
        }

        /// <summary>
        /// Gets or sets the authority that the issued token should be issued from.
        /// </summary>
        [JsonInclude]
        public string ExpectedAuthority { get; set; }

        /// <summary>
        /// Gets or sets the service that the issued token should be issued for.
        /// </summary>
        [JsonInclude]
        public string ExpectedService { get; set; }

        /// <summary>
        /// Gets or sets the DPoP proof key which was used to sign the token request.
        /// </summary>
        [JsonInclude]
        public string ProofKey { get; set; }

        /// <summary>
        /// Gets or sets the logging correlation ID.
        /// </summary>
        [JsonInclude]
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the start URL.
        /// </summary>
        /// <value>
        /// The start URL.
        /// </value>
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Matching the Duende property")]
        public string? StartUrl { get; set; }

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>
        /// The state.
        /// </value>
        public string State { get; set; }

        /// <summary>
        /// Gets or sets the code verifier.
        /// </summary>
        /// <value>
        /// The code verifier.
        /// </value>
        public string CodeVerifier { get; set; }

        /// <summary>
        /// Gets or sets the redirect URI.
        /// </summary>
        /// <value>
        /// The redirect URI.
        /// </value>
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Matching the Duende property")]
        public string RedirectUri { get; set; }

        /// <summary>
        /// Gets the error string, if any.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets the error description, if any.
        /// </summary>
        public string ErrorDescription { get; set; }

        /// <summary>
        /// Gets any extra properties for state.
        /// </summary>
        public IDictionary<string, string>? ExtraProperties { get; internal set; }

        /// <summary>
        /// Converts the <paramref name="state"/> to a JSON string.
        /// </summary>
        /// <param name="state">The <see cref="OAuthLoginState"/> to convert to json.</param>
        /// <returns>A string containing the <paramref name="state"/> as json.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        public static string ToJson(OAuthLoginState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            return JsonSerializer.Serialize(state, typeof(OAuthLoginState), SourceGenerationContext.Default);
        }

        /// <summary>
        /// Converts the <paramref name="json"/> to an instance of <see cref="OAuthLoginState"/>.
        /// </summary>
        /// <param name="json">The json to convert.</param>
        /// <returns>An instance of <see cref="OAuthLoginState"/> deserialized from the supplied <paramref name="json"/> string.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is null or white space.</exception>
        public static OAuthLoginState? FromJson(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            return JsonSerializer.Deserialize(json, typeof(OAuthLoginState), SourceGenerationContext.Default) as OAuthLoginState;
        }

        /// <summary>
        /// Converts this instance of <see cref="OAuthLoginState"/> to a JSON string.
        /// </summary>
        /// <returns>A string containing this instance of <see cref="OAuthLoginState"/> as json.</returns>
        public string ToJson()
        {
            return ToJson(this);
        }

        /// <summary>
        /// Gets the <see cref="AuthorizeState"/> for this instance.
        /// </summary>
        /// <returns>An instance of <see cref="AuthorizeState"/> representing this instance.</returns>
        public AuthorizeState ToAuthorizeState()
        {
            return new AuthorizeState()
            {
                StartUrl = StartUrl,
                State = State,
                CodeVerifier = CodeVerifier,
                RedirectUri = RedirectUri,
                Error = Error,
                ErrorDescription = ErrorDescription,
            };
        }

        /// <summary>
        /// Converts this instance of <see cref="OAuthLoginState"/> to a new instance of <see cref="AuthorizeState"/>.
        /// </summary>
        /// <param name="state">The <see cref="OAuthLoginState"/> to convert.</param>
        /// <returns>An instance of <see cref="AuthorizeState"/> representing this instance.</returns>
        public static implicit operator AuthorizeState?(OAuthLoginState state) => state?.ToAuthorizeState();

        /// <summary>
        /// Returns the hash code for this <see cref="OAuthLoginState"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="OAuthLoginState"/>.</returns>
        public override int GetHashCode() => (
            CodeVerifier,
            CorrelationId,
            RedirectUri,
            Error,
            ErrorDescription,
            ExpectedAuthority,
            ExpectedService,
            ProofKey,
            RedirectUri,
            StartUrl,
            State,
            ExtraProperties).GetHashCode();

        /// <summary>
        /// Indicates where an object is equal to this <see cref="OAuthLoginState"/>."/>
        /// </summary>
        /// <param name="obj">An object to compare to this <see cref="OAuthLoginState"/>.</param>
        /// <returns>
        /// true if this <see cref="OAuthLoginState"/> and the specified <paramref name="obj"/>> refer to the same object,
        /// this OAuthLoginState and the specified obj are both the same type of object and those objects are equal,
        /// or if this OAuthLoginStat and the specified obj are both null, otherwise, false.
        /// </returns>
        public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as OAuthLoginState);

        /// <summary>
        /// Indicates where this <see cref="OAuthLoginState"/> equals another."/>
        /// </summary>
        /// <param name="other">A <see cref="OAuthLoginState"/> or null to compare to this <see cref="OAuthLoginState"/>.</param>
        /// <returns>
        /// true if this <see cref="OAuthLoginState"/> and the specified <paramref name="other"/>> refer to the same object,
        /// this OAuthLoginState and the specified obj are both the same type of object and those objects are equal,
        /// or if this OAuthLoginState and the specified obj are both null, otherwise, false.
        /// </returns>
        public bool Equals([NotNullWhen(true)] OAuthLoginState? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (GetType() != other.GetType())
            {
                return false;
            }

            bool extraPropertiesComparisonResult = false;

            if (ExtraProperties is null && other.ExtraProperties is null)
            {
                extraPropertiesComparisonResult = true;
            }
            else if (ExtraProperties is null && other.ExtraProperties is not null)
            {
                extraPropertiesComparisonResult = false;
            }
            else if (ExtraProperties is not null && other.ExtraProperties is null)
            {
                extraPropertiesComparisonResult = false;
            }
            else if (ExtraProperties!.Count != other.ExtraProperties!.Count)
            {
                extraPropertiesComparisonResult = false;
            }
            else
            {
                extraPropertiesComparisonResult =
                    ExtraProperties.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).SequenceEqual(other.ExtraProperties.OrderBy(kvp => kvp.Key, StringComparer.Ordinal));
            }

            if (!extraPropertiesComparisonResult)
            {
                return false;
            }

            return string.Equals(CodeVerifier, other.CodeVerifier, StringComparison.Ordinal) &&
                  CorrelationId == other.CorrelationId &&
                  string.Equals(RedirectUri, other.RedirectUri, StringComparison.Ordinal) &&
                  string.Equals(Error, other.Error, StringComparison.Ordinal) &&
                  string.Equals(ErrorDescription, other.ErrorDescription, StringComparison.Ordinal) &&
                  string.Equals(ExpectedAuthority, other.ExpectedAuthority, StringComparison.Ordinal) &&
                  string.Equals(ExpectedService, other.ExpectedService, StringComparison.Ordinal) &&
                  string.Equals(ProofKey, other.ProofKey, StringComparison.Ordinal) &&
                  string.Equals(RedirectUri, other.RedirectUri, StringComparison.Ordinal) &&
                  string.Equals(StartUrl, other.StartUrl, StringComparison.Ordinal) &&
                  string.Equals(State, other.State, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether two specified <see cref="OAuthLoginState"/>s the same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="OAuthLoginState"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="OAuthLoginState"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, false.</returns>
        public static bool operator ==(OAuthLoginState? lhs, OAuthLoginState? rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                return false;
            }

            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Determines whether two specified <see cref="OAuthLoginState"/>s dot not have same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="OAuthLoginState"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="OAuthLoginState"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, false.</returns>
        public static bool operator !=(OAuthLoginState? lhs, OAuthLoginState? rhs) => !(lhs == rhs);
    }
}
