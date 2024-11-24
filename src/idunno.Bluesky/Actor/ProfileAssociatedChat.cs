// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    public record ProfileAssociatedChat
    {
        public ProfileAssociatedChat(AllowIncomingChat allowIncoming) => AllowIncoming = allowIncoming;

        public AllowIncomingChat AllowIncoming { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<AllowIncomingChat>))]
    public enum AllowIncomingChat
    {
        None = 0,
        All = 1,
        Following = 2
    }
}
