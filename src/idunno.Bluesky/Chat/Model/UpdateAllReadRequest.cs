// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace idunno.Bluesky.Chat.Model
{
    internal record UpdateAllReadRequest
    {
        public UpdateAllReadRequest(ConversationStatus status)
        {
            Status = status;
        }

        [JsonInclude]
        [JsonRequired]
        public ConversationStatus Status { get; init; }
    }
}
