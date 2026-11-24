// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Holds the parts of an OAuth token response that the Duende refresh result does not surface.
/// </summary>
internal sealed class TokenResponseCapture
{
    /// <summary>
    /// Gets the top level <c>scope</c> value of the most recent successful token response, if any.
    /// </summary>
    public string? Scope { get; set; }
}

/// <summary>
/// A handler which records the <c>scope</c> of successful token responses, so the authoritative granted scope can be
/// validated when the access token itself, which is opaque to clients, carries no scope claim.
/// </summary>
/// <param name="capture">Where captured values are stored.</param>
/// <param name="innerHandler">The handler that sends the request.</param>
internal sealed class TokenResponseCapturingHandler(TokenResponseCapture capture, HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (request.Method == HttpMethod.Post &&
            response.IsSuccessStatusCode &&
            response.Content.Headers.ContentType?.MediaType?.EndsWith("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                // ReadAsStringAsync buffers the content, so the caller can read it again.
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("access_token", out _))
                {
                    capture.Scope = document.RootElement.TryGetProperty("scope", out JsonElement scope) && scope.ValueKind == JsonValueKind.String
                        ? scope.GetString()
                        : null;
                }
            }
            catch (JsonException)
            {
                // Not a token response; Duende reports its own error for malformed bodies.
            }
        }

        return response;
    }
}
