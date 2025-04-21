// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// The result from a delete operation to the applyWrites API.
    /// </summary>
    public sealed record ApplyWritesDeleteResult : IApplyWritesResult
    {
        internal ApplyWritesDeleteResult()
        {
        }
    }
}
