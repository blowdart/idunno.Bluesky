﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections;
using System.Security.Cryptography;

namespace idunno.AtProto
{
    /// <summary>
    /// Utility class for Timestamp Identifiers (TID)
    /// </summary>
    public static class TimestampIdentifier
    {
        /// <summary>
        /// Generates a new timestamp identifier.
        /// </summary>
        /// <returns>A new timestamp identifier.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Bluesky normalizes to lower case.")]
        public static RecordKey Generate()
        {
            // https://atproto.com/specs/record-key

            BitArray timeStamp = new(64);

            // The top bit is always 0
            timeStamp.Set(0, false);

            TimeSpan duration = DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch;
            long microsecondsSinceEpoch = duration.Ticks / 10;

            // The next 53 bits represent microseconds since the UNIX epoch.
            BitArray microsecondsSinceEpochAsBits = new (BitConverter.GetBytes(microsecondsSinceEpoch));
            for (int i = 0; i <53; i++)
            {
                timeStamp.Set(i+1, microsecondsSinceEpochAsBits[i]);
            }

            // The final 10 bits are a random "clock identifier."
            BitArray randomIdentifier = new (RandomNumberGenerator.GetBytes(2));
            for (int i = 0; i < 10; i++)
            {
                timeStamp.Set(i + 1 + 53, randomIdentifier[i]);
            }

            // Now turn into a byte array.
            byte[] timeStampAsBytes = new byte[8];
            timeStamp.CopyTo(timeStampAsBytes, 0);

            // Encode to base32, and trim the padding.
            string base32Encoded = Base32Encoding.ToString(timeStampAsBytes).TrimEnd('=').ToLowerInvariant();

            return base32Encoded;
        }
    }
}
