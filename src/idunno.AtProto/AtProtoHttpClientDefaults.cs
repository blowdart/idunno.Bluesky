// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

/// <summary>
/// Holds the default request and response handlers for <see cref="AtProtoHttpClient{TResult}"/>.
/// </summary>
/// <remarks>
/// <para>
///   These live on a non-generic type so that every closed construction of <see cref="AtProtoHttpClient{TResult}"/> shares
///   the same instances, which allows a client to tell by reference whether a caller has attached a handler of its own.
/// </para>
/// </remarks>
internal static class AtProtoHttpClientDefaults
{
    /// <summary>
    /// The handler used when a caller has not attached their own function to be called when a request is about to be sent.
    /// </summary>
    internal static readonly Func<HttpRequestMessage, CancellationToken, Task> OnSendingRequest =
        (requestMessage, cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// The handler used when a caller has not attached their own function to be called when a response has been received.
    /// </summary>
    internal static readonly Func<HttpResponseMessage, CancellationToken, Task> OnResponseReceived =
        (responseMessage, cancellationToken) => Task.CompletedTask;
}
