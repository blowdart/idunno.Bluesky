// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.FireHose;

/// <summary>
/// Base class for converted frame bodies.
/// </summary>
/// <typeparam name="T">Type of the frame body.</typeparam>
/// <param name="Value">The value of the frame body.</param>
public record FrameBody<T>(T Value) where T : IFramePayload
{
}
