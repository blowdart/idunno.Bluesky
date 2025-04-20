// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Moderation;

using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Encapsulates a declaration of the existance of a labeler service.
    /// </summary>
    public sealed record LabelerDeclarationRecordValue : BlueskyRecordValue
    {
        // See https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/labeler/service.json

        /// <summary>
        /// Gets the policies the labeller publishes.
        /// </summary>
        public required LabelerPolicies Policies { get; init; }

        /// <summary>
        /// Gets the labels the labeler declares for itself, if any.
        /// </summary>
        public ICollection<SelfLabel> Labels { get; init; } = Array.Empty<SelfLabel>();

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the labeler declaration was created at.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the set of subject types (account, record, etc) this service accepts reports on.
        /// </summary>
        public ICollection<string>? SubjectTypes { get; init; } = Array.Empty <string>();

        /// <summary>
        /// Gets the set of record types (collection <see cref="Nsid"/>s) which can be reported to this service.
        /// If the value is null, as distinct from an empty collection the labeler accepts reports on any record type.
        /// </summary>
        public ICollection<Nsid>? SubjectCollections { get; init; }

    }
}
