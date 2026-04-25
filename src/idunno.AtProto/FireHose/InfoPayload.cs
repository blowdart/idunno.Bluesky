// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Represents a payload containing named information and an optional <paramref name="Message" />.
/// </summary>
/// <param name="Name">The name of the informational <paramref name="Message" />. The only known value is OutdatedCursor.</param>
/// <param name="Message">An optional message for the information frame.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="InfoPayload"/>.</para>
/// </remarks>
public record InfoPayload(string Name, string? Message) : IFramePayload, ICborConvertor<InfoPayload>
{
    internal InfoPayload(CBORObject cborObject) : this(
        cborObject["name"].AsString(),
        cborObject["message"]?.AsString())
    {
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null"/>.</exception>
    public static InfoPayload FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        return new InfoPayload(cborObject);
    }
}
