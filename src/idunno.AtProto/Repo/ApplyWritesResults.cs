// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Repo;

/// <summary>
/// The response from an applyWrites API call.
/// </summary>
public sealed record ApplyWritesResults
{
    internal ApplyWritesResults(ApplyWritesResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        Commit = response.Commit;

        List<IApplyWritesResult> results = [];
        foreach (ApplyWritesResponseBase commitResponse in response.Results ?? [])
        {
            switch (commitResponse)
            {
                case ApplyWritesCreateResponse createResponse:
                    results.Add(new ApplyWritesCreateResult(createResponse));
                    break;

                case ApplyWritesDeleteResponse _:
                    results.Add(new ApplyWritesDeleteResult());
                    break;

                case ApplyWritesUpdateResponse updateResponse:
                    results.Add(new ApplyWritesUpdateResult(updateResponse));
                    break;

                default:
                    break;
            }
        }

        Results = results.AsReadOnly();
    }

    /// <summary>
    /// Gets the commit for the applyWrites operation, if the server returned one.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The lexicon declares the commit as optional, so a server is free to apply the writes without reporting the
    ///   commit they landed in.
    /// </para>
    /// </remarks>
    public Commit? Commit { get; }

    /// <summary>
    /// Gets the results of the applyWrites operation.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The lexicon declares the results as optional, so this is empty rather than <see langword="null"/> when a server
    ///   applies the writes without reporting them individually.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<IApplyWritesResult> Results { get; }
}