// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Encapsulates the header information from a firehose message.
/// </summary>
public sealed record FrameHeader
{
    /// <summary>
    /// Creates a new instance of <see cref="FrameHeader"/> from the specified <paramref name="cborObject"/>.
    /// </summary>
    /// <param name="cborObject">The <paramref name="cborObject"/> containing the header information.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="cborObject"/> does not contain the required header information.</exception>
    public FrameHeader(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cborObject.Count);

        int operationAsInt = (cborObject["op"]?.AsInt32() ?? 0);

        if (!Enum.IsDefined(typeof(HeaderOperation), operationAsInt))
        {
            Operation = HeaderOperation.Unknown;
        }
        else
        {
            Operation = (HeaderOperation)operationAsInt;
        }

        Type = cborObject["t"]?.AsString();
    }

    /// <summary>
    /// Gets the operation the message encapsulates.
    /// </summary>
    public HeaderOperation Operation { get; init; }

    /// <summary>
    /// Gets the type of the header.
    /// </summary>
    public string? Type { get; init; }
}

/// <summary>
/// The operation type contained in a firehose <see cref="FrameHeader"/>.
/// </summary>
public enum HeaderOperation
{
    /// <summary>
    /// The header operation is an unknown frame.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The header indicates a regular frame.
    /// </summary>
    Frame = 1,

    /// <summary>
    /// The header indicates an AtError frame.
    /// </summary>
    Error = -1
}
