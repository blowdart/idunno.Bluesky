// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Specifies the application type advertised in OAuth client metadata.
/// </summary>
public enum BlueskyOAuthClientApplicationType
{
    /// <summary>
    /// Specifies a web application.
    /// </summary>
    Web,

    /// <summary>
    /// Specifies a native public application.
    /// </summary>
    Native
}
