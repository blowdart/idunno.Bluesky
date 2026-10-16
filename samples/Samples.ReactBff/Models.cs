// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Samples.ReactBff;

internal sealed record LoginRequest(string Handle);
internal sealed record PostRequest(string Text);
internal sealed record LoginResponse(string AuthorizationUrl);
internal sealed record CsrfResponse(string Token);
internal sealed record ApiError(string Error);
internal sealed record CreatedPost(string Uri, string Text);
internal sealed record SessionResponse(bool Authenticated, string? Did, DateTimeOffset? ExpiresAt, CreatedPost? CreatedPost);
internal sealed record TimelinePost(string Uri, string Author, string Text);
internal sealed record TimelineResponse(IReadOnlyList<TimelinePost> Posts);

[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(PostRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CsrfResponse))]
[JsonSerializable(typeof(ApiError))]
[JsonSerializable(typeof(CreatedPost))]
[JsonSerializable(typeof(SessionResponse))]
[JsonSerializable(typeof(TimelineResponse))]
internal partial class BffJsonContext : JsonSerializerContext;

internal sealed class BffException(int statusCode, string message) : Exception(message)
{
    internal int StatusCode { get; } = statusCode;
}
