// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides an object representation of an AT uniform resource identifier (AT URI) and easy access to the parts of the AT URI.
    /// </summary>
    /// <remarks>
    /// <para>See https://atproto.com/specs/at-uri-scheme</para>
    /// <para>For Bluesky a valid form is at-uri = at://{repo (did)}/{collection}/{rkey}</para>
    /// </remarks>
    [JsonConverter(typeof(Json.AtUriConverter))]
    public sealed class AtUri : IEquatable<AtUri>
    {
        private const string ProtocolAndSeparator = "at://";

        private static readonly Regex s_validationRegex =
            new (@"^at:\/\/(?<authority>[a-zA-Z0-9._:%-]+)(\/(?<collection>[a-zA-Z0-9-.]+)(\/(?<rkey>[a-zA-Z0-9._~:@!$&%')(*+,;=-]+))?)?(#(?<fragment>\/[a-zA-Z0-9._~:@!$&%')(*+,;=\-[\]/\\]*))?$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant,
                new TimeSpan(0, 0, 0, 5, 0));

        private static readonly Regex s_asciiRegex =
            new(@"^[a-zA-Z0-9._~:@!$&')(*+,;=%/-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant, new TimeSpan(0, 0, 0, 5, 0));

        private AtUri(string scheme, AtIdentifier authority, string? path, Nsid? collection, RecordKey? rKey)
        {
            Scheme = scheme;
            Authority = authority;
            AbsolutePath = path;
            Collection = collection;
            RecordKey = rKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtUri"/> class with the specified ATUri.
        /// </summary>
        /// <param name="s"></param>
        /// <exception cref="UriFormatException"></exception>
        public AtUri(string s)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(s);

            if (Parse(s, true, out AtUri? atUri))
            {
                Scheme = atUri!.Scheme;
                Authority = atUri.Authority;
                AbsolutePath = atUri.AbsolutePath;
                Collection = atUri.Collection;
                RecordKey = atUri.RecordKey;
            }
            else
            {
                throw new ArgumentException($"{s} is not a valid AT Uri", nameof(s));
            }
        }

        /// <summary>
        /// Gets the scheme name for this <see cref="AtUri"/>.
        ///
        /// This will always be "at" for a valid AtUri.
        /// </summary>
        /// <value>The normalized scheme component for this <see cref="AtUri"/>.</value>
        public string Scheme { get; internal set; } = string.Empty;

        /// <summary>
        /// Gets the <see cref="AtIdentifier"/> from this <see cref="AtUri"/>.
        /// </summary>
        public AtIdentifier Authority { get; internal set; }

        /// <summary>
        /// Gets the absolute path for this <see cref="AtUri"/>, if it contains an absolute path, otherwise null.
        /// </summary>
        public string? AbsolutePath { get; internal set; }

        /// <summary>
        /// Gets the <see cref="AtIdentifier"/> from this <see cref="AtUri"/> if the AtUri contains a repo (authority).
        /// </summary>
        [JsonIgnore]
        public AtIdentifier Repo => Authority;

        /// <summary>
        /// Returns the collection segment of the <see cref="AtUri"/> or null if the <see cref="AtUri"/> does not contain a collection.
        /// </summary>
        [JsonIgnore]
        public Nsid? Collection { get; internal set; }

        /// <summary>
        /// Returns the record key of the AT URI or null if the URI does not contain one.
        /// </summary>
        [JsonIgnore]
        public RecordKey? RecordKey { get; internal set; }

        /// <summary>
        /// Returns the hash code for this <see cref="AtUri"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="AtUri"/>.</returns>

        public override int GetHashCode() => (Scheme, Authority, AbsolutePath).GetHashCode();

        /// <summary>
        /// Indicates where an object is equal to this <see cref="AtUri"/>."/>
        /// </summary>
        /// <param name="obj">An object to compare to this <see cref="AtUri"/>.</param>
        /// <returns>
        /// true if this <see cref="AtUri"/> and the specified <paramref name="obj"/>> refer to the same object,
        /// this AtUri and the specified obj are both the same type of object and those objects are equal,
        /// or if this AtUri and the specified obj are both null, otherwise, false.
        /// </returns>
        public override bool Equals(object? obj) => Equals(obj as AtUri);

        /// <summary>
        /// Indicates where this <see cref="AtUri"/> equals another."/>
        /// </summary>
        /// <param name="other">A <see cref="AtUri"/> or null to compare to this <see cref="AtUri"/>.</param>
        /// <returns>
        /// true if this <see cref="AtUri"/> and the specified <paramref name="other"/>> refer to the same object,
        /// this AtUri and the specified obj are both the same type of object and those objects are equal,
        /// or if this AtUri and the specified obj are both null, otherwise, false.
        /// </returns>
        public bool Equals(AtUri? other)
        {
            if (other is null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (GetType() != other.GetType())
            {
                return false;
            }

            if (Authority is null && other.Authority is not null)
            {
                return false;
            }
            else if (Authority is not null && other.Authority is null)
            {
                return false;
            }
            else if (!Authority!.Equals(other.Authority))
            {
                return false;
            }

            return string.Equals(Scheme, other.Scheme, StringComparison.Ordinal) &&
                   string.Equals(AbsolutePath, other.AbsolutePath, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether two specified <see cref="AtUri"/>s the same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="AtUri"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="AtUri"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, false.</returns>
        public static bool operator ==(AtUri? lhs, AtUri? rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                return false;
            }

            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Determines whether two specified <see cref="AtUri"/>s dot not have same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="AtUri"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="AtUri"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, false.</returns>
        public static bool operator !=(AtUri? lhs, AtUri? rhs) => !(lhs == rhs);

        /// <summary>
        /// Serializes the component parts of the AT URI represented by this instance into a string.
        /// </summary>
        /// <returns>A string representation of the AT URI.</returns>
        public override string ToString()
        {
            StringBuilder atUriBuilder = new();

            if (!string.IsNullOrEmpty(Scheme))
            {
                atUriBuilder.Append(CultureInfo.InvariantCulture, $"{Scheme}://");
            }

            if (Authority is not null)
            {
                atUriBuilder.Append(CultureInfo.InvariantCulture, $"{Authority}");
            }

            if (!string.IsNullOrEmpty(AbsolutePath))
            {
                atUriBuilder.Append(CultureInfo.InvariantCulture, ($"{AbsolutePath}"));
            }

            return atUriBuilder.ToString();
        }

        /// <summary>
        /// Creates a <see cref="AtUri"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AtUri(string s) => new(s);

        /// <summary>
        /// Creates a <see cref="AtUri"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtUri FromString(string s) => s;

        /// <summary>
        /// Converts the string representation of an identifier to its <see cref="AtUri"/> equivalent.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="s">A string containing the id to convert.</param>
        /// <param name="result">
        /// When this method returns contains the <see cref="AtUri"/> equivalent of the
        /// string contained in s, or null if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
        /// is null or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
        /// supplied in result will be overwritten.
        /// </param>
        /// <returns>true if s was converted successfully; otherwise, false.</returns>
        public static bool TryParse(string s, [NotNullWhen(true)] out AtUri? result)
        {
            if (string.IsNullOrEmpty(s))
            {
                result = null;
                return false;
            }

            return Parse(s, false, out result);
        }

        private static bool Parse(string s, bool throwOnError, out AtUri? result)
        {
            // Validation comes from https://github.com/bluesky-social/atproto/blob/290a7e67b8e6417b00352cc1d54bac006c2f6f93/packages/syntax/src/aturi_validation.ts#L99

            result = null;

            if (!s.StartsWith(ProtocolAndSeparator, StringComparison.InvariantCulture))
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException(nameof(s) + " has an invalid scheme.");
                }
                else
                {
                    return false;
                }
            }

            if (s.OccurrenceCount('?') > 0)
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException($"AT URIs cannot contain a query part.");
                }
                else
                {
                    return false;
                }
            }

            if (s.OccurrenceCount('#') > 0)
                {
                if (throwOnError)
                {
                    throw new AtUriFormatException($"AT URIs cannot contain a fragment.");
                }
                else
                {
                    return false;
                }
            }

            if (!s_asciiRegex.Match(s).Success)
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException("AT URIs can only contain ASCII.");
                }
                else
                {
                    return false;
                }
            }

            string[] uriParts = s.Split('/');

            if (uriParts.Length >= 3 && (uriParts[0] != "at:" || uriParts[1].Length != 0))
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException("AT URIs must start with \"at://.");
                }
                else
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(uriParts[2]))
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException($"{nameof(s)} contains no authority or path.");
                }
                else
                {
                    return false;
                }
            }

            if (uriParts[2].StartsWith("did:", StringComparison.InvariantCultureIgnoreCase))
            {
                if (!Did.TryParse(uriParts[2], out Did? did) || did is null)
                {
                    if (throwOnError)
                    {
                        throw new AtUriFormatException($"{uriParts[2]} is not a valid DID.");
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!Handle.TryParse(uriParts[2], out Handle? handle) || handle is null)
                {
                    if (throwOnError)
                    {
                        throw new AtUriFormatException($"{uriParts[2]} is not a valid handle.");
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            string remainingStringToParse = s;

            if (string.IsNullOrWhiteSpace(remainingStringToParse))
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException(nameof(s) + " is empty or only contains whitespace.");
                }
                else
                {
                    return false;
                }
            }

            Match regexValidationResult = s_validationRegex.Match(s);

            if (!regexValidationResult.Success || regexValidationResult.Groups.Count == 0)
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException($"{s} is not a valid AT URI.");
                }
                else
                {
                    return false;
                }
            }

            if (regexValidationResult.Groups.ContainsKey("collection") && !string.IsNullOrEmpty(regexValidationResult.Groups["collection"].Value))
            {
                bool nsidParseResult = Nsid.TryParse(regexValidationResult.Groups["collection"].Value, out Nsid? _);
                if (!nsidParseResult)
                {
                    if (throwOnError)
                    {
                        throw new AtUriFormatException($"Collection segment, {regexValidationResult.Groups["collection"].Value}, must be a valid NSID.");
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (s.Length > 8 * 1024)
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException($"{s} is too long.");
                }
                else
                {
                    return false;
                }
            }

            string scheme = @"at";
            AtIdentifier? authority;
            string? absolutePath = null;
            Nsid? collection = null;
            RecordKey? recordKey = null;

            remainingStringToParse = remainingStringToParse[ProtocolAndSeparator.Length..];

            if (!remainingStringToParse.Contains('/', StringComparison.InvariantCulture))
            {
                if (remainingStringToParse.Length == 0)
                {
                    if (throwOnError)
                    {
                        throw new AtUriFormatException($"{s} contains no authority or path.");
                    }
                    else
                    {
                        return false;
                    }
                }

                if (!AtIdentifier.TryParse(remainingStringToParse, out authority))
                {
                    if (throwOnError)
                    {
                        throw new AtUriFormatException($"{s} does not have a valid authority.");
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                int firstSlashPosition = remainingStringToParse.IndexOf('/', StringComparison.InvariantCulture);
                absolutePath = remainingStringToParse[firstSlashPosition..];

                if (!AtIdentifier.TryParse(remainingStringToParse[..firstSlashPosition], out authority))
                {
                    if (throwOnError)
                    {
                        throw new AtUriFormatException($"{s} does not have a valid authority.");
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (absolutePath is not null && absolutePath.Contains('/', StringComparison.InvariantCulture))
            {
                string[] pathSegments = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (pathSegments.Length > 2)
                {
                    throw new AtUriFormatException($"{s} has too many segments");
                }

                if (pathSegments.Length >= 1)
                {
                    bool isSegmentValidNsid = Nsid.Parse(pathSegments[0], throwOnError, out collection);

                    if (!isSegmentValidNsid)
                    {
                        return false;
                    }
                }

                if (pathSegments.Length == 2)
                {
                    bool isSegmentValidRecordKey = RecordKey.Parse(pathSegments[1], throwOnError, out recordKey);

                    if (!isSegmentValidRecordKey)
                    {
                        return false;
                    }
                }
            }

            if (authority is null)
            {
                if (throwOnError)
                {
                    throw new AtUriFormatException($"{s} has no authority.");
                }
                else
                {
                    return false;
                }
            }

            result = new AtUri(scheme, authority, absolutePath, collection, recordKey);

            return true;
        }
    }
}
