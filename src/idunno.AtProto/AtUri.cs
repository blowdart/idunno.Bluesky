// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using idunno.AtProto.Bluesky;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides an object representation of an AT uniform resource identifier (AT URI) and easy access to the parts of the AT URI.
    /// </summary>
    /// <remarks>
    /// See https://atproto.com/specs/at-uri-scheme
    /// For Bluesky at-uri = at://{repo (did)}/{collection}/{rkey}
    /// </remarks>
    public sealed class AtUri
    {
        private const string ProtocolAndSeparator = "at://";

        private AtUri(string scheme, string authority, string? path, string? query, string? fragment)
        {
            Scheme = scheme;
            Authority = authority;
            AbsolutePath = path;
            Query = query;
            Fragment = fragment;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtUri"/> class with the specified ATUri.
        /// </summary>
        /// <param name="s"></param>
        /// <exception cref="UriFormatException"></exception>
        public AtUri(string s)
        {
            if (Parse(s, true, out AtUri? atUri))
            {
                Scheme = atUri!.Scheme;
                Authority = atUri!.Authority;
                AbsolutePath = atUri!.AbsolutePath;
                Query = atUri!.Query;
                Fragment = atUri!.Fragment;
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
        /// Gets any query information included in the AT URI, including the leading '?' character if not empty.
        /// </summary>
        /// <value>
        /// Any query information included in the specified URI.
        /// </value>
        public string? Query { get; internal set; }

        /// <summary>
        /// Gets the URI fragment, including the leading '#' character if not empty.
        /// </summary>
        /// <value>
        /// URI fragment information.
        /// </value>
        public string? Fragment { get; internal set; }

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
        public string? RKey
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

            if (!string.IsNullOrEmpty(Query))
            {
                atUriBuilder.Append(CultureInfo.InvariantCulture, $"{Query}");
            }

            if (!string.IsNullOrEmpty(Fragment))
            {
                atUriBuilder.Append(CultureInfo.InvariantCulture, $"{Fragment}");
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
            return Parse(s, false, out atUri);
        }

        private static bool Parse(string s, bool throwOnError, out AtUri? atUri)
        {
            atUri = null;
            string remainingStringToParse = s;

            if (string.IsNullOrWhiteSpace(remainingStringToParse))
            {
                if (throwOnError)
                {
                    throw new UriFormatException(nameof(s) + " is empty or only contains whitespace.");
                }
                else
                {
                    return false;
                }
            }

            if (!remainingStringToParse.StartsWith(ProtocolAndSeparator, StringComparison.InvariantCulture))
            {
                if (throwOnError)
                {
                    throw new UriFormatException(nameof(s) + " has an invalid scheme.");
                }
                else
                {
                    return false;
                }
            }

            string scheme = @"at";
            string? fragment = null;
            string? query = null;
            string authority;
            string? absolutePath = null;

            remainingStringToParse = remainingStringToParse[ProtocolAndSeparator.Length..];

            if (remainingStringToParse.OccurrenceCount('#') > 1)
            {
                if (throwOnError)
                {
                    throw new UriFormatException(nameof(s) + " contains multiple fragment separators.");
                }
                else
                {
                    return false;
                }
            }
            else if (remainingStringToParse.OccurrenceCount('#') == 1)
            {
                int fragmentSeparatorPosition = remainingStringToParse.IndexOf('#', StringComparison.InvariantCulture);
                fragment = remainingStringToParse[(fragmentSeparatorPosition)..];
                remainingStringToParse = remainingStringToParse[..fragmentSeparatorPosition];
            }

            if (remainingStringToParse.OccurrenceCount('?') > 1)
            {
                if (throwOnError)
                {
                    throw new UriFormatException(nameof(s) + " contains multiple query separators.");
                }
                else
                {
                    return false;
                }
            }
            else if (remainingStringToParse.OccurrenceCount('?') == 1)
            {
                int querySeparatorPosition = remainingStringToParse.IndexOf('?', StringComparison.InvariantCulture);
                query = remainingStringToParse[(querySeparatorPosition)..];
                remainingStringToParse = remainingStringToParse.Substring(0, querySeparatorPosition);
            }

            if (!remainingStringToParse.Contains('/', StringComparison.InvariantCulture))
            {
                if (remainingStringToParse.Length == 0)
                {
                    if (throwOnError)
                    {
                        throw new UriFormatException(nameof(s) + " contains no authority or path.");
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
                absolutePath = remainingStringToParse[(firstSlashPosition)..];
            }

            atUri = new AtUri(scheme, authority, absolutePath, query, fragment);
            return true;
        }
    }
}
