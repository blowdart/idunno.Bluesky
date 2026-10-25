// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Reports categorized service failures without caller identifiers, evidence or signing material.</summary>
internal static partial class LabelIssuanceLog
{
    /// <summary>Emits a rejection category without including the subject, caller reason or exception details.</summary>
    [LoggerMessage(1, LogLevel.Warning, "Label service rejected {Operation} with {Code}.")]
    internal static partial void Rejected(ILogger logger, string operation, LabelServiceErrorCode code);
}
