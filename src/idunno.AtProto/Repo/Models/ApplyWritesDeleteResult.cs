// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates the result from a delete operation to the applyWrites API.
    /// </summary>
    public sealed record ApplyWritesDeleteResult : ApplyWritesResultBase
    {
        [JsonConstructor]
        internal ApplyWritesDeleteResult()
        {
        }
    }
}
