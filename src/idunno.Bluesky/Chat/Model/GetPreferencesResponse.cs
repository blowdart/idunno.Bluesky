// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Notifications;

namespace idunno.Bluesky.Chat.Model;

internal sealed record GetPreferencesResponse([property: JsonRequired] Preferences Preferences)
{
}