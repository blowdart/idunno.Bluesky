// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Contains constant values related to Bluesky authentication.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The correlation cookie name.
    /// </summary>
    public const string CorrelationCookieName = "_idunno_bluesky_Correlation";

    /// <summary>
    /// The return URL parameter name.
    /// </summary>
    public const string ReturnUrlKey = "_returnUrl";

    internal const string CorrelationPurpose = "idunno.Bluesky.AspNet.Authentication.OAuthCorrelation";
}
