// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Provides methods for decoding a <see cref="CBORObject"/> to <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The class to convert to.</typeparam>
public interface ICborConvertor<out T> where T : class
{
    /// <summary>
    /// Converts the specified <see cref="CBORObject"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <param name="cborObject">The <see cref="CBORObject"/> to convert.</param>
    /// <returns>An instance of <typeparamref name="T"/> created from <paramref name="cborObject"/>.</returns>
    static abstract T FromCBORObject(CBORObject cborObject);
}
