// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Records service authorization checks and optionally denies selected checks.</summary>
/// <remarks>
/// <para>The helper returns explicit versioned grants by default so tests exercise the service's mandatory
/// authorization path without embedding an authentication implementation.</para>
/// </remarks>
internal sealed class TestLabelIssuanceAuthorizer : ILabelIssuanceAuthorizer
{
    private readonly ConcurrentQueue<LabelAuthorizationRequest> _requests = new();

    /// <summary>Gets or sets the predicate selecting authorization checks to reject.</summary>
    internal Func<LabelAuthorizationRequest, bool>? Deny { get; set; }

    /// <summary>Gets or sets the version recorded for each successfully authorized scope.</summary>
    internal Func<LabelAuthorizationRequest, string> GrantVersion { get; set; } = _ => "test-grant-v1";

    /// <summary>Gets a stable observation of checks for assertions about admission and recovered scope.</summary>
    internal IReadOnlyCollection<LabelAuthorizationRequest> Requests => _requests.ToArray();

    /// <summary>Records each admission or resolved-scope check before returning the configured denial or versioned grant.</summary>
    public Task<LabelServiceResult<LabelAuthorizationGrant>> AuthorizeAsync(
        LabelAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Enqueue(request);
        LabelServiceResult<LabelAuthorizationGrant> result = Deny?.Invoke(request) is true
            ? LabelServiceResult<LabelAuthorizationGrant>.Failure(
                new(
                    code: LabelServiceErrorCode.Unauthorized,
                    message: "The test authorizer denied this check."))
            : LabelServiceResult<LabelAuthorizationGrant>.Success(new(GrantVersion(request)));
        return Task.FromResult(result);
    }
}
