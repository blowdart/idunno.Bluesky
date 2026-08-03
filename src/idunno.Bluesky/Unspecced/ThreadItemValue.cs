// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// Represents a thread item in a post thread.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ThreadItemPost), "app.bsky.unspecced.defs#threadItemPost")]
[JsonDerivedType(typeof(ThreadItemNoUnauthenticated), "app.bsky.unspecced.defs#threadItemNoUnauthenticated")]
[JsonDerivedType(typeof(ThreadItemNotFound), "app.bsky.unspecced.defs#threadItemNotFound")]
[JsonDerivedType(typeof(ThreadItemBlocked), "app.bsky.unspecced.defs#threadItemBlocked")]
public record ThreadItemValue
{
}
