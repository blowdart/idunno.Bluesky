// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Error information from a fire hose frame.
/// </summary>
/// <param name="Error">The error, if any.</param>
/// <param name="Message">The error message, if any.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="FrameError"/> with the specified <paramref name="Error" /> and <paramref name="Message" />, if any.</para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not ghost parameters.")]
public sealed record FrameError(string? Error, string? Message) : IFramePayload, ICborConvertor<FrameError>
{
    internal FrameError(CBORObject cborObject) :this(
        cborObject["error"]?.AsString(),
        cborObject["message"]?.AsString())
    {

    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null"/>.</exception>
    public static FrameError FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        return new FrameError(cborObject);
    }
}
