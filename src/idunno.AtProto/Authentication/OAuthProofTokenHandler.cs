// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

using Microsoft.AspNetCore.WebUtilities;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Signs OAuth backchannel requests with destination-bound nonces and captures updates on every response.
/// </summary>
/// <param name="proofKey">The existing session proof key, including its signing algorithm.</param>
/// <param name="nonces">The nonce state for this proof key.</param>
/// <param name="assertionFactory">The factory for fresh confidential-client assertions, if configured.</param>
/// <param name="innerHandler">The configured transport handler.</param>
internal sealed class OAuthProofTokenHandler(
    string proofKey,
    DPoPNonceStore nonces,
    Func<Task<ClientAssertion>>? assertionFactory,
    HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    private readonly DefaultDPoPProofTokenFactory _proofFactory = new(proofKey);

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? sentNonce = SetProof(request);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string? returnedNonce = response.GetDPoPNonce();
        nonces.Set(request.RequestUri!, returnedNonce);

        if (!string.IsNullOrWhiteSpace(returnedNonce) &&
            !string.Equals(sentNonce, returnedNonce, StringComparison.Ordinal) &&
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            response.Dispose();

            if (assertionFactory is not null && request.Content is not null)
            {
                ClientAssertion? assertion = await assertionFactory().ConfigureAwait(false);
                if (assertion is not null)
                {
                    string body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    Dictionary<string, Microsoft.Extensions.Primitives.StringValues> fields = QueryHelpers.ParseQuery(body);
                    fields["client_assertion"] = assertion.Value;
                    fields["client_assertion_type"] = assertion.Type;
                    HttpContent originalContent = request.Content;
                    request.Content = new FormUrlEncodedContent(fields.SelectMany(field =>
                        field.Value.Select(value => new KeyValuePair<string, string>(field.Key, value!))));
                    originalContent.Dispose();
                }
            }

            SetProof(request);
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            nonces.Set(request.RequestUri!, response.GetDPoPNonce());
        }

        return response;
    }

    /// <summary>
    /// Creates a fresh proof using only the nonce belonging to this request's destination.
    /// </summary>
    private string? SetProof(HttpRequestMessage request)
    {
        string? nonce = nonces.Get(request.RequestUri!);
        DPoPProof proof = _proofFactory.CreateProofToken(new DPoPProofRequest
        {
            Method = request.Method.Method,
            Url = request.GetDPoPUrl(),
            DPoPNonce = nonce,
            AccessToken = request.Headers.Authorization is { } authorization &&
                string.Equals(authorization.Scheme, "DPoP", StringComparison.OrdinalIgnoreCase)
                ? authorization.Parameter
                : null
        });
        request.SetDPoPProofToken(proof.ProofToken);

        return nonce;
    }
}
