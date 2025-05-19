// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// The response from an applyWrites API call.
    /// </summary>
    public sealed record ApplyWritesResults
    {
        internal ApplyWritesResults(ApplyWritesResponse response)
        {
            Commit = response.Commit;

            List<IApplyWritesResult> results = [];
            foreach (ApplyWritesResponseBase commitResponse in response.Results)
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
        /// Gets the commit for the applyWrites operation.
        /// </summary>
        public Commit Commit { get; }

        /// <summary>
        /// Gets the results of the applyWrites operation.
        /// </summary>
        public IReadOnlyCollection<IApplyWritesResult> Results { get; }
    }
}
