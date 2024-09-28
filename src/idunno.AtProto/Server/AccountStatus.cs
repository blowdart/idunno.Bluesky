// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    [JsonConverter(typeof(JsonStringEnumConverter<AccountStatus>))]
    public enum AccountStatus
    {
        None = 0,
        Takendown = 1,
        Suspended = 2,
        Deactivated = 3
    }
}
