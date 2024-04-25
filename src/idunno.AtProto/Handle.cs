// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto
{
    /// <summary>
    /// Implements an AT Protocol Handle.
    ///
    /// Handles are a less-permanent identifier for accounts, when compared to <see cref="Did" />s.
    /// </summary>
    /// <remarks>
    /// See https://atproto.com/specs/handle for further details.
    /// </remarks>
    public class Handle : AtIdentifier
    {
        private const int MaximumLength = 253;

        private static readonly Regex s_validate = new(
            @"^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$",
            RegexOptions.None,
            TimeSpan.FromMilliseconds(100));

        private static readonly IReadOnlyList<string> s_invalidTLDs =
            new List<string>()
            {
                ".alt",
                ".arpa",
                ".example",
                ".internal",
                ".invalid",
                ".local",
                ".localhost",
                ".onion"
            };

        private Handle(string s, bool parse)
        {
            Value = string.Empty;

            // Normalize
            s = s.ToLowerInvariant();

            if (parse)
            {
                if (Parse(s, true, out Handle? _))
                {
                    Value = s;
                }
            }
            else
            {
                Value = s;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Handle"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to create a handle from.</param>
        [JsonConstructor]
        public Handle(string s) : this(s, true)
        {
        }

        /// <summary>
        /// The special handle value that can be used by APIs to indicate that there is no bi-directionally valid handle for a given DID.
        /// This handle can not be used in most situations (search queries, API requests, etc).
        /// </summary>
        /// <value>
        /// The special handle value that can be used by APIs to indicate that there is no bi-directionally valid handle for a given DID.
        /// </value>
        public static Handle InvalidHandle { get; } = new Handle("handle.invalid", false);

        /// <summary>
        /// Gets the normalized value of the handle.
        /// </summary>
        [JsonPropertyName("handle")]
        public string Value { get; }

        /// <summary>
        /// Converts the Handle to its equivalent string representation.
        /// </summary>
        /// <returns>The string representation of the value of this instance.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Converts the string representation of an identifier to its <see cref="Handle"/> equivalent.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="s">A string containing the id to convert.</param>
        /// <param name="result">
        /// When this method returns contains the <see cref="Handle"/> equivalent of the
        /// string contained in s, or null if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
        /// is null or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
        /// supplied in result will be overwritten.
        /// </param>
        /// <returns>true if s was converted successfully; otherwise, false.</returns>
        public static bool TryParse(string s, out Handle? handle)
        {
            return Parse(s, false, out handle);
        }

        private static bool Parse(string s, bool throwOnError, out Handle? handle)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                if (throwOnError)
                {
                    throw new ArgumentNullException(nameof(s));
                }
                else
                {
                    handle = null;
                    return false;
                }
            }

            if (s.Length > MaximumLength)
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" length is greater than {MaximumLength}.", nameof(s));
                }
                else
                {
                    handle = null;
                    return false;
                }
            }

            if (s.StartsWith('.') || s.EndsWith('.'))
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" cannot begin or end with '.'.", nameof(s));
                }
                else
                {
                    handle = null;
                    return false;
                }
            }

            if (!s.Contains('.', StringComparison.InvariantCulture))
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" is not a valid hostname.", nameof(s));
                }
                else
                {
                    handle = null;
                    return false;
                }
            }

            string tld = s.Substring(s.LastIndexOf('.'));
            if (s_invalidTLDs.Contains(tld))
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{tld}\" is a disallowed TLD.", nameof(s));
                }
                else
                {
                    handle = null;
                    return false;
                }
            }


            if (!s_validate.IsMatch(s))
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" does not validate as a handle.", nameof(s));
                }
                else
                {
                    handle = null;
                    return false;
                }
            }

            handle = new Handle(s, false);
            return true;
        }
    }
}
