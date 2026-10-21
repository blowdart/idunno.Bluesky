// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Labels;

/// <summary>
/// Specifies the elliptic curve used by a label signing key.
/// </summary>
public enum LabelSigningCurve
{
    /// <summary>
    /// The NIST P-256 curve, also known as secp256r1.
    /// </summary>
    P256,

    /// <summary>
    /// The secp256k1 curve.
    /// </summary>
    Secp256k1
}
