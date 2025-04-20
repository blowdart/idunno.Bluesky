// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Moderation
{
    /// <summary>
    /// Encapsulates a moderation report.
    /// </summary>
    public sealed record ModerationReport
    {
        /// <summary>
        /// Gets the report identifier.
        /// </summary>
        public required ulong Id { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the report was created at.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the type of the reason for the report.
        /// </summary>
        public required string ReasonType { get; init; }

        /// <summary>
        /// Gets any extended reasoning for the report.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the <see cref="Did"/> of the actor who submitted the report.
        /// </summary>
        public required Did ReportedBy { get; init; }

        /// <summary>
        /// Gets the <see cref="SubjectType"/> of the report.
        /// </summary>
        public required SubjectType Subject { get; init; }
    }
}
