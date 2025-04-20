// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Moderation;

namespace idunno.AtProto.Admin
{
    /// <summary>
    /// Defines a reference to a repository.
    /// </summary>
    public sealed record RepoReference : SubjectType
    {
        /// <summary>
        /// The <see cref="AtProto.Did"/> of the repository.
        /// </summary>
        public required Did Did { get; init; }
    }
}
