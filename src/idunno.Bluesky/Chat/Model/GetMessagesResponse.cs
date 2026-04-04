// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model;

[SuppressMessage("Performance", "CA1812", Justification = "Used in GetMessages.")]
[method: JsonConstructor]
internal sealed class GetMessagesResponse(string? cursor, ICollection<MessageViewBase> messages)
{
    [JsonInclude]
    public string? Cursor { get; init; } = cursor;

    [JsonInclude]
    [JsonRequired]
    public ICollection<MessageViewBase> Messages { get; init; } = [.. messages];
}
