// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;
using idunno.Bluesky;

namespace Samples.ReactBff;

internal interface ISessionClient : IDisposable
{
    string Did { get; }
    DateTimeOffset TokenExpiresAt { get; }
    Task<bool> RefreshAsync(CancellationToken cancellationToken);
    Task<TimelineResponse> GetTimelineAsync(CancellationToken cancellationToken);
    Task<StrongReference> CreatePostAsync(string text, CancellationToken cancellationToken);
    Task DeletePostAsync(StrongReference post, CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
}

internal sealed class BlueskySessionClient(BlueskyAgent agent) : ISessionClient
{
    public string Did => agent.Did!.Value;
    public DateTimeOffset TokenExpiresAt => agent.Credentials!.ExpiresOn;

    public Task<bool> RefreshAsync(CancellationToken cancellationToken) =>
        agent.RefreshCredentials(cancellationToken: cancellationToken);

    public async Task<TimelineResponse> GetTimelineAsync(CancellationToken cancellationToken)
    {
        var result = await agent.GetTimeline(limit: 20, cancellationToken: cancellationToken);
        if (!result.Succeeded)
        {
            throw UpstreamFailure("Timeline retrieval", (int)result.StatusCode);
        }

        return new TimelineResponse([.. result.Result.Select(entry => new TimelinePost(
            entry.Post.Uri.ToString(),
            entry.Post.Author.Handle.ToString(),
            entry.Post.Record.Text ?? string.Empty))]);
    }

    public async Task<StrongReference> CreatePostAsync(string text, CancellationToken cancellationToken)
    {
        var result = await agent.Post(text, extractFacets: false, cancellationToken: cancellationToken);
        if (!result.Succeeded)
        {
            throw UpstreamFailure("Post creation (check your account before retrying)", (int)result.StatusCode);
        }

        return result.Result.StrongReference;
    }

    public async Task DeletePostAsync(StrongReference post, CancellationToken cancellationToken)
    {
        var result = await agent.DeletePost(post, cancellationToken: cancellationToken);
        if (!result.Succeeded)
        {
            throw UpstreamFailure("Post deletion", (int)result.StatusCode);
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken) => agent.Logout(cancellationToken);
    public void Dispose() => agent.Dispose();

    private static BffException UpstreamFailure(string operation, int status) =>
        new(StatusCodes.Status502BadGateway, $"{operation} failed (upstream HTTP {status}).");
}
