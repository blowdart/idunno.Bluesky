﻿// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace IdentityModel.OidcClient.DPoP;

/// <exclude />
/// <summary>
/// Models the request information to create a DPoP proof token
/// </summary>
public class DPoPProofRequest
{
    /// <summary>
    /// The HTTP URL of the request
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Forked Code")]
    public string Url { get; set; } = default!;

    /// <summary>
    /// The HTTP method of the request
    /// </summary>
    public string Method { get; set; } = default!;

    /// <summary>
    /// The nonce value for the DPoP proof token.
    /// </summary>
    public string? DPoPNonce { get; set; }

    /// <summary>
    /// The access token
    /// </summary>
    public string? AccessToken { get; set; }
}
