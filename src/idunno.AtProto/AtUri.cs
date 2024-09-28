// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides an object representation of an AT uniform resource identifier (AT URI) and easy access to the parts of the AT URI.
    /// </summary>
    /// <remarks>
    /// See https://atproto.com/specs/at-uri-scheme
    /// For Bluesky at-uri = at://{repo (did)}/{collection}/{rkey}
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

        private static readonly Regex s_recordKeyValidationRegex =
            new(@"^[a-zA-Z0-9_~.:-]{1,512}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, new TimeSpan(0, 0, 0, 5, 0));

        private AtUri(string scheme, string authority, string? path)
        {
            Scheme = scheme;
            Authority = authority;
            AbsolutePath = path;
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
                Authority = atUri!.Authority;
                AbsolutePath = atUri!.AbsolutePath;
            }
        }

        /// <summary>
        /// Gets the scheme name for the AT URI represented by this instance.
        ///
        /// This will always be "at" for a valid AT URI.
        /// </summary>
        /// <value>The scheme component for the AT URI represented by this instance, converted to lower case.</value>
        public string Scheme { get; internal set; } = string.Empty;

        /// <summary>
        /// Gets authority component of the AT URI represented by this instance.
        /// </summary>
        /// <value>The authority component of the AT URI represented by this instance.</value>
        public string Authority { get; internal set; } = string.Empty;

        /// <summary>
        /// Gets the absolute path for the AT URI represented by this instance.
        /// </summary>
        /// <value>
        /// The absolute path to the resource.
        /// </value>
        public string? AbsolutePath { get; internal set; }

        /// <summary>
        /// Gets either the <see cref="Did"/> or <see cref="Handle"/> from the AT URI if the URI contains one,
        /// otherwise returns null.
        /// </summary>
        /// <value>
        /// The <see cref="AtIdentifier"/> of the URI or null if the URI does not contain one.
        /// </value>
        [JsonIgnore]
        public AtIdentifier? Repo
        {
            get
            {
                if (string.IsNullOrEmpty(Authority))
                {
                    return null;
                }

                _ = AtIdentifier.TryParse(Authority, out AtIdentifier? atIdentifier);
                return atIdentifier;
            }
        }

        /// <summary>
        /// Returns the collection segment of the AT URI or null if the URI does not contain one.
        /// </summary>
        /// <remarks>
        /// The collection segment of the AT URI or null if the URI does not contain one
        /// </remarks>
        [JsonIgnore]
        public string? Collection
        {
            get
            {
                if (AbsolutePath is not null && AbsolutePath.Contains('/', StringComparison.InvariantCulture) && Repo is not null)
                {
                    string[] pathSegments = AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (pathSegments.Length >= 1)
                    {
                        return pathSegments[0];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Returns the record key of the AT URI or null if the URI does not contain one.
        /// </summary>
        /// <remarks>
        /// The record key segment of the AT URI or null if the URI does not contain one
        /// </remarks>
        [JsonIgnore]
        public string? Rkey
        {
            get
            {
                if (AbsolutePath is not null && AbsolutePath.Contains('/', StringComparison.InvariantCulture) && Repo is not null && !string.IsNullOrEmpty(Collection))
                {
                    string[] pathSegments = AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (pathSegments.Length >= 2)
                    {
                        return pathSegments[1];
                    }
                }

                return null;
            }
        }

        public override int GetHashCode() => (Scheme, Authority, AbsolutePath).GetHashCode();

        public override bool Equals(object? obj) => Equals(obj as AtUri);

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

            return string.Equals(Scheme, other.Scheme, StringComparison.Ordinal) &&
                   string.Equals(Authority, other.Authority, StringComparison.Ordinal) &&
                   string.Equals(AbsolutePath, other.AbsolutePath, StringComparison.Ordinal);
        }

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

            if (!string.IsNullOrEmpty(Authority))
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
        /// Converts the string representation of an identifier to its <see cref="AtUri"/> equivalent.
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
        public static bool TryParse(string s, out AtUri? atUri)
        {
            if (string.IsNullOrEmpty(s))
            {
                atUri = null;
                return false;
            }

            return Parse(s, false, out atUri);
        }

        private static bool Parse(string s, bool throwOnError, out AtUri? atUri)
        {
            // Validation comes from https://github.com/bluesky-social/atproto/blob/290a7e67b8e6417b00352cc1d54bac006c2f6f93/packages/syntax/src/aturi_validation.ts#L99

            atUri = null;

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
                    throw new AtUriFormatException($"\"{s}\" is not a valid AT URI.");
                }
                else
                {
                    return false;
                }
            }

            if (regexValidationResult.Groups.ContainsKey("collection") &&
                !string.IsNullOrEmpty(regexValidationResult.Groups["collection"].Value))
            {
                bool result = Nsid.TryParse(regexValidationResult.Groups["collection"].Value, out Nsid? _);
                if (!result)
                {
                    if (throwOnError)
                    {
                        throw new AtUriFormatException($"ATURI collection path segment, {regexValidationResult.Groups["collection"].Value}, must be a valid NSID.");
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
                    throw new AtUriFormatException($"AT URI is too long.");
                }
                else
                {
                    return false;
                }
            }

            string scheme = @"at";
            string authority;
            string? absolutePath = null;

            remainingStringToParse = remainingStringToParse[ProtocolAndSeparator.Length..];

            if (!remainingStringToParse.Contains('/', StringComparison.InvariantCulture))
            {
                if (remainingStringToParse.Length == 0)
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
                authority = remainingStringToParse;
            }
            else
            {
                int firstSlashPosition = remainingStringToParse.IndexOf('/', StringComparison.InvariantCulture);
                authority = remainingStringToParse[..firstSlashPosition];
                absolutePath = remainingStringToParse[firstSlashPosition..];
            }

            atUri = new AtUri(scheme, authority, absolutePath);

            if (atUri.Rkey is not null)
            {
                string rkey = atUri.Rkey;

                if (rkey.Length > 512 || rkey.Length < 1)
                {
                    if (throwOnError)
                    {
                        throw new RecordKeyFormatException("Record key length must be between 1 and 512 characters");
                    }
                    else
                    {
                        atUri = null;
                        return false;
                    }
                }

                if (!s_recordKeyValidationRegex.Match(rkey).Success)
                {
                    if (throwOnError)
                    {
                        throw new RecordKeyFormatException("Record key syntax is invalid.");
                    }
                    else
                    {
                        atUri = null;
                        return false;
                    }
                }

                if (rkey == "." || rkey == "..")
                {
                    if (throwOnError)
                    {
                        throw new RecordKeyFormatException("Record key cannot be \".\" or \"..\".");
                    }
                    else
                    {
                        atUri = null;
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
