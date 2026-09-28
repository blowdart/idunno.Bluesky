// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// Sends the web socket upgrade through an <see cref="HttpMessageInvoker"/> and rejects a response which was not answered
/// from the uri that was requested, because the invoker followed a redirect.
/// </summary>
/// <remarks>
/// <para>The client the firehose builds for itself does not follow redirects, but a client supplied by an application might.
/// By the time a redirected response arrives the request has already been sent to the redirected host, so this cannot stop
/// that, but it does stop the connection being used.</para>
/// </remarks>
/// <param name="inner">The invoker to send requests through. It is not disposed with this handler.</param>
internal sealed class RedirectRejectingHandler(HttpMessageInvoker inner) : HttpMessageHandler
{
    /// <summary>
    /// The message given when a connection is refused because it was redirected.
    /// </summary>
    internal const string RedirectedMessage = "The firehose server redirected the connection. Redirects are not followed, so configure the host the server redirects to instead.";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Captured before sending, as a handler which follows a redirect rewrites the uri on the request it was given.
        Uri? requested = request.RequestUri;

        // The web socket client asks an HttpClient for the headers alone, so the upgraded stream is not buffered.
        HttpResponseMessage response = inner is HttpClient client
            ? await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false)
            : await inner.SendAsync(request, cancellationToken).ConfigureAwait(false);

        Uri? answered = response.RequestMessage?.RequestUri;

        if (requested is not null && answered is not null && answered != requested)
        {
            response.Dispose();
            throw new FirehoseConnectionException(null, RedirectedMessage, null);
        }

        return response;
    }
}
