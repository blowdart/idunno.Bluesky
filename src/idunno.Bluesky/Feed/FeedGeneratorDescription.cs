// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A description of a feed generator.
    /// </summary>
    public sealed record FeedGeneratorDescription
    {
        [JsonConstructor]
        internal FeedGeneratorDescription(Did did, IReadOnlyList<GeneratorFeed> feeds, Links links)
        {
            Did = did;
            Feeds = feeds;
            Links = links;
        }

        /// <summary>
        /// The <see cref="Did"/> of the feed generator.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }

        /// <summary>
        /// A read-only list of feeds the generator provides.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<GeneratorFeed> Feeds { get; init; }

        /// <summary>
        /// Links for the feed generator.
        /// </summary>
        public Links Links { get; init; }
    }

    /// <summary>
    /// A feed from a feed generator
    /// </summary>
    public sealed record GeneratorFeed
    {
        [JsonConstructor]
        internal GeneratorFeed(AtUri uri)
        {
            Uri = uri;
        }

        /// <summary>
        /// Gets the <see cref="AtUri" /> for the feed.
        /// </summary>
        [JsonInclude]
        public AtUri Uri { get; init; }
    }

    /// <summary>
    /// Links for a feed generator.
    /// </summary>
    public sealed record Links
    {
        [JsonConstructor]
        internal Links(Uri? privacyPolicy, Uri? termsOfService)
        {
            PrivacyPolicy = privacyPolicy;
            TermsOfService = termsOfService;
        }

        /// <summary>
        /// Gets a URI to the server's privacy policy.
        /// </summary>
        [JsonInclude]
        public Uri? PrivacyPolicy { get; internal set; }

        /// <summary>
        /// Gets a URI to the server's terms of service.
        /// </summary>
        [JsonInclude]
        public Uri? TermsOfService { get; internal set; }
    }
}
