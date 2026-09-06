// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Unspecced;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Indicates that the thread item is blocked.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Used in json polymorphism, record has no properties.")]
public sealed record ThreadItemBlocked : ThreadItemValue
{
}
