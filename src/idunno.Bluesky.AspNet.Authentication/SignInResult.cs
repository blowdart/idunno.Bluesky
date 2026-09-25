// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Encapsulates the result of a sign-in attempt.
/// </summary>
/// <param name="Succeeded">Flag indicating if the sign-in succeeded.</param>
/// <param name="MissingQueryString">Flag indicating if the sign-in failed due to a lack of a query string on the associated request.</param>
/// <param name="MissingCorrelationState">Flag indicating if the sign-in failed as the correlation state could not be found in the state store.</param>
/// <param name="ErrorProcessingOAuth2Response">Flag indicating if the sign-in failed because the OAuth response was invalid.</param>
/// <param name="OAuthLoginState">The <see cref="OAuthLoginState"/> associated with the sign-in, if any.</param>
public record SignInResult(
    bool Succeeded,
    bool MissingQueryString = false,
    bool MissingCorrelationState = false,
    bool ErrorProcessingOAuth2Response = false,
    OAuthLoginState? OAuthLoginState = null)
{
}
