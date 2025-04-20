// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Moderation.Model
{
    internal sealed record CreateReportRequest
    {
        public required string ReasonType { get; init; }

        public string? Reason { get; init; }

        public required SubjectType Subject { get; init; }
    }
}
