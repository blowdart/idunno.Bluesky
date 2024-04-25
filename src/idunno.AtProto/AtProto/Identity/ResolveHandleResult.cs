// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Identity
{
    /// <summary>
    /// The results of a resolve handle request.
    /// </summary>
    /// <param name="Did">A document identifier returned by handle resolution.</param>
    internal record ResolveHandleResult(Did Did)
    {
    }
}
