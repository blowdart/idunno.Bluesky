// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates the result from a create operation to the applyWrites API.
    /// </summary>
    internal sealed record ApplyWritesCreateResponse(AtUri Uri, Cid Cid) : ApplyWritesResponseBase
    {
        public string? ValidationStatus { get; init; }
    }
}
