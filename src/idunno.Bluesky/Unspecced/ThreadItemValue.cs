// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// Represents a thread item in a post thread.
/// </summary>
/// <remarks>
/// <para>
/// A thread item whose <c>$type</c> discriminator is not one of the types known to this version of the SDK
/// deserializes to a bare <see cref="ThreadItemValue"/> rather than causing the containing response to fail.
/// Callers should pattern match on the derived types they understand and ignore any instance which is
/// exactly a <see cref="ThreadItemValue"/>.
/// </para>
/// </remarks>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ThreadItemPost), "app.bsky.unspecced.defs#threadItemPost")]
[JsonDerivedType(typeof(ThreadItemNoUnauthenticated), "app.bsky.unspecced.defs#threadItemNoUnauthenticated")]
[JsonDerivedType(typeof(ThreadItemNotFound), "app.bsky.unspecced.defs#threadItemNotFound")]
[JsonDerivedType(typeof(ThreadItemBlocked), "app.bsky.unspecced.defs#threadItemBlocked")]
public record ThreadItemValue
{
}
