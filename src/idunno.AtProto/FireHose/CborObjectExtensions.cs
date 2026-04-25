// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Helper functions for <see cref="CBORObject"/>
/// </summary>
public static class CborObjectExtensions
{
    /// <summary>
    /// Returns the value of the specified <paramref name="cborObject"/> as a <see cref="DateTimeOffset"/> if it is convertible,
    /// otherwise returns <see langword="null"/>.
    /// </summary>
    /// <param name="cborObject">The <see cref="CBORObject"/> to try to convert.</param>
    /// <returns>A <see cref="DateTimeOffset"/> if it is convertible.</returns>
    /// <exception cref="CBORException">Thrown when <paramref name="cborObject"/> cannot be converted.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="cborObject"/> is a number.</exception>
    public static DateTimeOffset ToDateTimeOffset(this CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        ArgumentOutOfRangeException.ThrowIfEqual(cborObject.IsNumber, true);

        string value = cborObject.AsString();

        if (string.IsNullOrEmpty(value))
        {
            throw new CBORException("cborObject value is null");
        }

        if (!DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTimeOffset dateTimeOffset))
        {
            throw new CBORException($"{value} is not a valid DateTimeOffset");
        }

        return dateTimeOffset;
    }

    /// <summary>
    /// Returns the value of the specified <paramref name="cborObject"/> as a <see cref="Cid"/> if it is convertible,
    /// otherwise returns <see langword="null"/>.
    /// </summary>
    /// <param name="cborObject">The <see cref="CBORObject"/> to convert.</param>
    /// <returns>A <see cref="Cid"/> if it is convertible.</returns>
    public static Cid? ToCid(this CBORObject cborObject)
    {
        if (cborObject is null || cborObject.IsNull)
        {
            return null;
        }

        if (cborObject.Type == CBORType.ByteString)
        {
            byte[] cidBytes = cborObject.GetByteString();

            if (cidBytes[0] != 0)
            {
                return null;
            }

            return new Cid(cidBytes[1..]);
        }
        else if (cborObject.Type != CBORType.TextString)
        {
            return new Cid(cborObject.AsString());
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the value of the specified <paramref name="cborObject"/> as a <see cref="RecordKey"/> if it is convertible,
    /// otherwise returns <see langword="null"/>.
    /// </summary>
    /// <param name="cborObject">The <see cref="CBORObject"/> to convert.</param>
    /// <returns>A <see cref="RecordKey"/> if it is convertible.</returns>
    public static RecordKey? ToRecordKey(this CBORObject cborObject)
    {
        if (cborObject is null || cborObject.IsNull)
        {
            return null;
        }

        try
        {
            return new RecordKey(cborObject.AsString());
        }
        catch (CBORException)
        {
            return null;
        }
    }
}
