// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto.Repo.Models
{
    [SuppressMessage("Performance", "CA1812", Justification = "Using during CreateBlob.")]
    internal sealed record CreateBlobResponse(Blob Blob)
    {
    }
}
