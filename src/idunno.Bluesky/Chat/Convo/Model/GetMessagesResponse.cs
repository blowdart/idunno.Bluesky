// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

namespace idunno.Bluesky.Chat.Convo.Model;

[SuppressMessage("Performance", "CA1812", Justification = "Used in GetMessages.")]
[method: JsonConstructor]
internal sealed class GetMessagesResponse(string? cursor, ICollection<MessageViewBase> messages, ICollection<ProfileViewBasic>? relatedProfiles)
{
    [JsonInclude]
    public string? Cursor { get; set; } = cursor;

    [JsonInclude]
    [JsonRequired]
    public ICollection<MessageViewBase> Messages { get; set; } = [.. messages];

    [JsonInclude]
    public ICollection<ProfileViewBasic>? RelatedProfiles { get; set; } = relatedProfiles is null ? null : [.. relatedProfiles];
}