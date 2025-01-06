// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Linq;
using System.Net.Http;

namespace IdentityModel.OidcClient.DPoP;

/// <summary>
/// Extensions for HTTP request/response messages
/// </summary>
public static class DPoPExtensions
{
    /// <summary>
    /// Sets the DPoP nonce request header if nonce is not null. 
    /// </summary>
    public static void SetDPoPProofToken(this HttpRequestMessage request, string? proofToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // remove any old headers
        request.Headers.Remove(OidcConstants.HttpHeaders.DPoP);
        // set new header
        request.Headers.Add(OidcConstants.HttpHeaders.DPoP, proofToken);
    }

    /// <summary>
    /// Reads the DPoP nonce header from the response
    /// </summary>
    public static string? GetDPoPNonce(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);


        var nonce = response.Headers
            .FirstOrDefault(x => x.Key == OidcConstants.HttpHeaders.DPoPNonce)
            .Value?.FirstOrDefault();
        return nonce;
    }

    /// <summary>
    /// Returns the URL without any query params
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "Forked code")]
    public static string GetDPoPUrl(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.RequestUri!.Scheme + "://" + request.RequestUri!.Authority + request.RequestUri!.LocalPath;
    }
}
