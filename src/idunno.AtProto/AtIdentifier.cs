// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Base class for <see cref="Did"/> and <see cref="Handle"/> implementations.
    /// </summary>
    [JsonConverter(typeof(Json.AtIdentifierConverter))]
    public abstract class AtIdentifier
    {
        /// <summary>
        /// Converts the string representation of an identifier to its <see cref="Did"/> or <see cref="Handle"/> equivalent.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="s">A string containing the id to convert.</param>
        /// <param name="result">
        /// When this method returns contains the <see cref="AtIdentifier"/> equivalent of the
        /// string contained in s, or null if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
        /// is null or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
        /// supplied in result will be overwritten.
        /// </param>
        /// <returns>true if s was converted successfully; otherwise, false.</returns>
        public static bool TryParse(string s, out AtIdentifier? result)
        {
            if (!string.IsNullOrEmpty(s))
            {
                if (Did.TryParse(s, out Did? did))
                {
                    result = did;
                    return true;
                }

                if (Handle.TryParse(s, out Handle? handle))
                {
                    result = handle;
                    return true;
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Converts the AtIdentifier to its equivalent string representation.
        /// </summary>
        /// <returns>The string representation of the value of this instance.</returns>
        public abstract override string ToString();
    }
}
