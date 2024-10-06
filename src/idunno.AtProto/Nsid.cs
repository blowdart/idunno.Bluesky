// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto
{
    /// <summary>
    /// A class representing a namespace identifier.
    /// </summary>
    /// <remarks>
    /// <para>See https://atproto.com/specs/nsid for details.</para>
    /// </remarks>
    [JsonConverter(typeof(Json.NsidConverter))]
    public class Nsid
    {
        private readonly string _value;

        private static readonly Regex s_validationRegex =
            new(@"^[a-zA-Z]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+(\.[a-zA-Z]([a-zA-Z]{0,61}[a-zA-Z])?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant, new TimeSpan(0, 0, 0, 5, 0));

        private static readonly Regex s_characters =
            new("^[a-zA-Z0-9.-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant, new TimeSpan(0, 0, 0, 0, 5));

        private Nsid(string s, bool validate)
        {
            if (validate)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(s);
                if (Parse(s, true, out _))
                {
                    _value = s;
                }
                else
                {
                    throw new NsidFormatException($"{s} is not a valid nsid.");
                }
            }
            else
            {
                _value = s;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Nsid"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to create an <see cref="Nsid"/> from.</param>
        /// <exception cref="NsidFormatException">Thrown if <paramref name="s"/> is not a valid NSID.</exception>
        [JsonConstructor]
        public Nsid(string s) : this(s, true)
        {
        }

        /// <summary>
        /// Gets the NSID authority for this instance.
        /// </summary>
        /// <value>
        /// The NSID authority for this instance.
        /// </value>
        [JsonIgnore]
        public string Authority => string.Join('.', _value.Split('.')[..^1]);

        /// <summary>
        /// Gets the NSID name for this instance.
        /// </summary>
        /// <value>
        /// The NSID name for this instance.
        /// </value>
        [JsonIgnore]
        public string Name => _value.Split('.')[^1];

        /// <summary>
        /// Returns a string representation of the <see cref="Nsid"/> current instance.
        /// </summary>
        /// <returns>a string representation of the <see cref="Nsid"/> current instance.</returns>
        public override string ToString() => _value;

        /// <summary>
        /// Creates an <see cref="Nsid"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Nsid(string s) => new(s);

        /// <summary>
        /// Creates an <see cref="Nsid"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Nsid FromString(string s) => s;

        /// <summary>
        /// Converts the string representation of an identifier to its <see cref="Nsid"/> equivalent.
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
        public static bool TryParse(string s, out Nsid? result)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(s);
            return Parse(s, false, out result);
        }

        /// <summary>
        /// Returns the hash code for this <see cref="Nsid"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="Nsid"/>.</returns>
        public override int GetHashCode() => _value.GetHashCode(StringComparison.Ordinal);

        /// <summary>
        /// Indicates where an object is equal to this <see cref="Nsid"/>."/>
        /// </summary>
        /// <param name="obj">An object to compare to this <see cref="Nsid"/>.</param>
        /// <returns>
        /// true if this <see cref="Nsid"/> and the specified <paramref name="obj"/>> refer to the same object,
        /// this Nsid and the specified obj are both the same type of object and those objects are equal,
        /// or if this Nsid and the specified obj are both null, otherwise, false.
        /// </returns>
        public override bool Equals(object? obj) => Equals(obj as Nsid);

        /// <summary>
        /// Indicates where this <see cref="Nsid"/> equals another."/>
        /// </summary>
        /// <param name="other">A <see cref="Nsid"/> or null to compare to this <see cref="Nsid"/>.</param>
        /// <returns>
        /// true if this <see cref="Nsid"/> and the specified <paramref name="other"/>> refer to the same object,
        /// this Nsid and the specified obj are both the same type of object and those objects are equal,
        /// or if this Nsid and the specified obj are both null, otherwise, false.
        /// </returns>
        public bool Equals(Nsid? other)
        {
            if (other is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (GetType() != other.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            return (string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                string.Equals(Authority, other.Authority, StringComparison.Ordinal));
        }

        internal static bool Parse(string s, bool throwOnError, out Nsid? result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(s))
            {
                if (throwOnError)
                {
                    ArgumentNullException.ThrowIfNullOrWhiteSpace(s);
                }
                else
                {
                    return false;
                }
            }

            if (!s_validationRegex.Match(s).Success)
            {
                if (throwOnError)
                {
                    throw new NsidFormatException($"{s} is not a valid nsid.");
                }
                else
                {
                    return false;
                }
            }

            if (!s_characters.Match(s).Success)
            {
                if (throwOnError)
                {
                    throw new NsidFormatException($"{s} is not a valid nsid.");
                }
                else
                {
                    return false;
                }
            }

            if (s.Length > 253 + 1 + 63)
            {
                if (throwOnError)
                {
                    throw new NsidFormatException($"{s} is too long.");
                }
                else
                {
                    return false;
                }
            }

            string[] labels = s.Split('.');

            if (labels.Length < 3)
            {
                if (throwOnError)
                {
                    throw new NsidFormatException($"{s} needs at least three parts.");
                }
                else
                {
                    return false;
                }
            }

            for (int i=0; i<labels.Length; i++)
            {
                string label = labels[i];

                if (label.Length == 0)
                {
                    if (throwOnError)
                    {
                        throw new NsidFormatException($"NSID parts can not be empty.");
                    }
                    else
                    {
                        return false;
                    }
                }

                if (label.Length > 63)
                {
                    if (throwOnError)
                    {
                        throw new NsidFormatException($"NSID part too long (max 63 chars)");
                    }
                    else
                    {
                        return false;
                    }
                }

                if (label.EndsWith('-') || label.StartsWith('-'))
                {
                    if (throwOnError)
                    {
                        throw new NsidFormatException($"NSID parts can not start or end with hyphen.");
                    }
                    else
                    {
                        return false;
                    }
                }

                if (i == 0 && char.IsDigit(label[0]))
                {
                    if (throwOnError)
                    {
                        throw new NsidFormatException($"NSID first part may not start with a digit.");
                    }
                    else
                    {
                        return false;
                    }
                }

                if (i + 1 == labels.Length)
                {
                    if (!label.IsOnlyAsciiLetters())
                    {
                        if (throwOnError)
                        {
                            throw new NsidFormatException($"NSID name part must be only letters.");
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            result = new Nsid(s, false);
            return true;
        }
    }
}
