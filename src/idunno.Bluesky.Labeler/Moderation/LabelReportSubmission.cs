// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains bounded report intent without trusting a caller-supplied reporter identity.</summary>
public sealed class LabelReportSubmission
{
    /// <summary>Initializes a new instance of the <see cref="LabelReportSubmission"/> class.</summary>
    /// <param name="subject">The exact submitted account or immutable record reference.</param>
    /// <param name="reasonType">The open report-reason identifier.</param>
    /// <param name="reason">The optional report context.</param>
    /// <param name="modToolName">The optional reporting tool name.</param>
    /// <param name="modToolMetadata">The optional bounded tool metadata.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A field is invalid or metadata is supplied without a tool name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A text or metadata field exceeds its limit.</exception>
    /// <exception cref="JsonException">The tool metadata exceeds the nesting limit.</exception>
    /// <remarks><para>Context is limited to 2,000 graphemes and 20,000 UTF-8 bytes. Tool names are
    /// locally bounded to 1,024 UTF-8 bytes and metadata to 8,192 UTF-8 bytes and 16 levels of nesting.</para></remarks>
    public LabelReportSubmission(
        LabelReportSubject subject,
        string reasonType,
        string? reason = null,
        string? modToolName = null,
        JsonElement? modToolMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        Subject = subject;
        ReasonType = LabelInput.ValidateText(reasonType, nameof(reasonType), 1024);
        if (reason is not null)
        {
            LabelInput.ValidateText(value: reason, parameterName: nameof(reason), maximumBytes: 20000, allowBlank: true);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.GetGraphemeLength(), 2000);
        }

        Reason = reason;
        if (modToolName is not null)
        {
            ModToolName = LabelInput.ValidateText(modToolName, nameof(modToolName), 1024);
        }

        if (modToolMetadata is { } metadata)
        {
            if (modToolName is null)
            {
                throw new ArgumentException("Tool metadata requires a tool name.", nameof(modToolMetadata));
            }

            string json = metadata.GetRawText();
            ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(json), 8192);
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            ModToolMetadata = document.RootElement.Clone();
        }
    }

    /// <summary>Gets the immutable submitted reference.</summary>
    /// <value>The account DID or record URI and CID, without network rewriting.</value>
    public LabelReportSubject Subject { get; }
    /// <summary>Gets the open reason identifier.</summary>
    /// <value>The exact reason, including future identifiers.</value>
    public string ReasonType { get; }
    /// <summary>Gets the optional confidential context.</summary>
    /// <value>The reporter's retained text.</value>
    public string? Reason { get; }
    /// <summary>Gets the optional reporting tool name.</summary>
    /// <value>The bounded tool identifier.</value>
    public string? ModToolName { get; }
    /// <summary>Gets the optional immutable tool metadata.</summary>
    /// <value>The retained arbitrary metadata.</value>
    public JsonElement? ModToolMetadata { get; }
}
