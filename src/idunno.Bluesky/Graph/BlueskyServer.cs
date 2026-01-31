// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Graph.Model;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky
{
    public static partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/app-bsky-feed-get-blocks
        private const string GetBlocksEndpoint = "/xrpc/app.bsky.graph.getBlocks";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-followers
        private const string GetFollowersEndpoint = "/xrpc/app.bsky.graph.getFollowers";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-followers
        private const string GetFollowsEndpoint = "/xrpc/app.bsky.graph.getFollows";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-known-followers
        private const string GetKnownFollowersEndpoint = "/xrpc/app.bsky.graph.getKnownFollowers";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-list-blocks
        private const string GetListBlocksEndpoint = "/xrpc/app.bsky.graph.getListBlocks";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-list-mutes
        private const string GetListMutesEndpoint = "/xrpc/app.bsky.graph.getListMutes";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-list
        private const string GetListEndpoint = "/xrpc/app.bsky.graph.getList";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-lists
        private const string GetListsEndpoint = "/xrpc/app.bsky.graph.getLists";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-mutes
        private const string GetMutesEndpoint = "/xrpc/app.bsky.graph.getMutes";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-relationships
        private const string GetRelationshipsEndpoint = "/xrpc/app.bsky.graph.getRelationships";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-starter-pack
        private const string GetStarterPackEndpoint = "/xrpc/app.bsky.graph.getStarterPack";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-starter-packs
        private const string GetStarterPacksEndpoint = "/xrpc/app.bsky.graph.getStarterPacks";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-suggested-follows-by-actor
        private const string GetSuggestedFollowsByActorEndpoint = "/xrpc/app.bsky.graph.getSuggestedFollowsByActor";

        // https://docs.bsky.app/docs/api/app-bsky-graph-mute-actor-list
        private const string MuteActorListEndpoint = "/xrpc/app.bsky.graph.muteActorList";

        // https://docs.bsky.app/docs/api/app-bsky-graph-mute-actor
        private const string MuteActorEndpoint = "/xrpc/app.bsky.graph.muteActor";

        // https://docs.bsky.app/docs/api/app-bsky-graph-mute-thread
        private const string MuteThreadEndpoint = "/xrpc/app.bsky.graph.muteThread";

        // https://docs.bsky.app/docs/api/app-bsky-graph-unmute-actor-list
        private const string UnmuteActorListEndpoint = "/xrpc/app.bsky.graph.unmuteActorList";

        // https://docs.bsky.app/docs/api/app-bsky-graph-unmute-actor
        private const string UnmuteActorEndpoint = "/xrpc/app.bsky.graph.unmuteActor";

        // https://docs.bsky.app/docs/api/app-bsky-graph-unmute-thread
        private const string UnmuteThreadEndpoint = "/xrpc/app.bsky.graph.unmuteThread";

        // https://docs.bsky.app/docs/api/app-bsky-graph-get-actor-starter-packs
        private const string GetActorStarterPacksEndpoint = "/xrpc/app.bsky.graph.getActorStarterPacks";

        /// <summary>
        /// Enumerates which accounts the requesting account is currently blocking. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of followers that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the blocks from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetBlocks(
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetBlocksResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetBlocksResponse> response = await client.Get(
                service: service,
                endpoint: $"{GetBlocksEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    new PagedViewReadOnlyCollection<ProfileView>(
                        new List<ProfileView>(response.Result.Blocks).AsReadOnly(),
                        cursor: response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }

        }

        /// <summary>
        /// Enumerates accounts which follow a specified account (actor).
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be enumerated.</param>
        /// <param name="limit">The maximum number of followers that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the followers from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Followers>> GetFollowers(
            AtIdentifier actor,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor!)}");

            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetFollowersResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetFollowersResponse> response = await client.Get(
                service: service,
                endpoint: $"{GetFollowersEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Followers>(
                    new Followers(
                        subject: response.Result.Subject,
                        followers: new List<ProfileView>(response.Result.Followers).AsReadOnly(),
                        cursor: response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Followers>(
                    new Followers(subject: null, followers: [], null),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets a paged list of accounts whom an actor follows.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be enumerated.</param>
        /// <param name="limit">The maximum number of followers that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the follows from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Follows>> GetFollows(
            AtIdentifier actor,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor!)}");

            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetFollowsResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetFollowsResponse> response = await client.Get(
                service,
                $"{GetFollowsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions : BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Follows>(
                    new Follows(
                        subject: response.Result.Subject,
                        follows: new List<ProfileView>(response.Result.Follows).AsReadOnly(),
                        cursor: response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Follows>(
                    new Follows(subject: null, follows: [], null),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets a paged list of accounts which follow a specified account (actor) and are followed by the viewer. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be enumerated.</param>
        /// <param name="limit">The maximum number of followers that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the known followers from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Followers>> GetKnownFollowers(
            AtIdentifier actor,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor!)}");

            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetFollowersResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetFollowersResponse> response = await client.Get(
                service,
                $"{GetKnownFollowersEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Followers>(
                    new Followers(
                        subject: response.Result.Subject,
                        followers: new List<ProfileView>(response.Result.Followers).AsReadOnly(),
                        cursor: response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Followers>(
                    new Followers(subject: null, followers: [], null),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a paged list of mod lists that the requesting account (actor) is blocking. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the list blocks from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetListBlocks(
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetListBlocksResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetListBlocksResponse> response = await client.Get(
                service,
                $"{GetListBlocksEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>(
                    new PagedViewReadOnlyCollection<ListView>(response.Result.Lists, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a paged list of mod lists that the requesting account (actor) is muting. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the list mutes from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetListMutes(
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetListBlocksResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetListBlocksResponse> response = await client.Get(
                service,
                $"{GetListMutesEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>(
                    new PagedViewReadOnlyCollection<ListView>(response.Result.Lists, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a <see cref="ListView"/> of a Bluesky list and a paginated collection of <see cref="ListItemView"/>s of its items.
        /// </summary>
        /// <param name="list">The <see cref="AtUri"/> of the list to get.</param>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the list from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<ListViewWithItems>> GetList(
            AtUri list,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();

            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"list={Uri.EscapeDataString(list.ToString())}&");

            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetListResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetListResponse> response = await client.Get(
                service,
                $"{GetListEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ListViewWithItems>(
                    new ListViewWithItems(response.Result.List, response.Result.Items, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ListViewWithItems>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a paged list of lists that created by the specified <paramref name="actor"/>.
        /// </summary>
        /// <param name="actor">The actor whose lists to enumerate.</param>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the lists from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetLists(
            AtIdentifier actor,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor!)}");

            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetListsResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetListsResponse> response = await client.Get(
                service,
                $"{GetListsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>(
                    new PagedViewReadOnlyCollection<ListView>(response.Result.Lists, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a paged list of muted profiles for the current user. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the mutes from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetMutes(
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();

            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"limit={limit}");

                if (cursor is not null)
                {
                    queryStringBuilder.Append('&');
                }
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"cursor = {Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetMutesResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetMutesResponse> response = await client.Get(
                service,
                $"{GetMutesEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    new PagedViewReadOnlyCollection<ProfileView>(response.Result.Mutes, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        // Mismatches lexicon definition - https://github.com/bluesky-social/atproto/issues/2919

        /// <summary>
        /// Enumerates public relationships between one account, and a list of other accounts.
        /// </summary>
        /// <param name="actor">The <see cref="Did"/> of the actor whose follows should be enumerated.</param>
        /// <param name="others">A list of other accounts to be related back to <paramref name="actor"/>.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="others"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="others"/> is empty, or has &gt; 30 entries.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<ActorRelationships>> GetRelationships(
            Did actor,
            ICollection<Did> others,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(others);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentOutOfRangeException.ThrowIfLessThan(others.Count, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(others.Count, 30);

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");

            foreach (AtIdentifier other in others)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&others={Uri.EscapeDataString(other.ToString())}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetRelationshipsResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetRelationshipsResponse> response = await client.Get(
                service,
                $"{GetRelationshipsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                Dictionary<Did, Relationship> returnValue = [];

                foreach (RelationshipType relationshipType in response.Result.Relationships)
                {
                    if (relationshipType is Relationship relationship)
                    {
                        returnValue.Add(relationship.Did, relationship);
                    }
                }

                return new AtProtoHttpResult<ActorRelationships>(
                    new ActorRelationships(response.Result.Actor, returnValue),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ActorRelationships>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets a view of a starter pack.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the starter pack to view.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<StarterPackView>> GetStarterPack(
            AtUri uri,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<GetStarterPackResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetStarterPackResponse> response = await client.Get(
                service,
                $"{GetStarterPackEndpoint}?starterPack={Uri.EscapeDataString(uri.ToString())}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<StarterPackView>(
                    response.Result.StarterPack,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<StarterPackView>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets a basic view of the specified starter packs.
        /// </summary>
        /// <param name="uris">A collection of <see cref="AtUri"/>s for the starter packs to view.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the starter packs from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uris"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when number <paramref name="uris"/> is &lt; 1 or &gt; 25.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>>> GetStarterPacks(
            ICollection<AtUri> uris,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uris);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentOutOfRangeException.ThrowIfLessThan(uris.Count, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(uris.Count, 25);

            StringBuilder queryStringBuilder = new();
            foreach (AtUri uri in uris)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"uris={Uri.EscapeDataString(uri.ToString())}&");
            }
            queryStringBuilder.Length--;

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetStarterPacksResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetStarterPacksResponse> response = await client.Get(
                service,
                $"{GetStarterPacksEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>>(
                    new List<StarterPackViewBasic>(response.Result.StarterPacks).AsReadOnly(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>>(
                    new List<StarterPackViewBasic>().AsReadOnly(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Enumerates follows similar to a given account (<paramref name="actor"/>). Expected use is to recommend additional accounts immediately after following one account.
        /// </summary>
        /// <param name="actor">The account to enumerate suggested follows for.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the follows from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<SuggestedActors>> GetSuggestedFollowsByActor(
            AtIdentifier actor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<SuggestedActors> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<SuggestedActors> response = await client.Get(
                service,
                $"{GetSuggestedFollowsByActorEndpoint}?actor={Uri.EscapeDataString(actor.ToString())}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Creates a mute relationship for the specified list of accounts. Requires authentication.
        /// </summary>
        /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to mute.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to create the mutes on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> MuteActorList(
            AtUri listUri,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listUri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<EmptyResponse> response = await client.Post(
                service,
                $"{MuteActorListEndpoint}",
                new MuteActorListRequest(listUri),
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Creates a mute relationship for the specified account. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to mute</param>
        /// <param name="service">The <see cref="Uri"/> of the service cerate the mute on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> MuteActor(
            AtIdentifier actor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<EmptyResponse> response = await client.Post(
                service,
                $"{MuteActorEndpoint}",
                new MuteActorRequest(actor),
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Mutes a thread preventing notifications from the thread and any of its children. Requires authentication.
        /// </summary>
        /// <param name="rootUri">The <see cref="AtUri"/> of the thread to mute</param>
        /// <param name="service">The <see cref="Uri"/> of the service cerate the mute on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootUri"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> MuteThread(
            AtUri rootUri,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rootUri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<EmptyResponse> response = await client.Post(
                service,
                $"{MuteThreadEndpoint}",
                new MuteThreadRequest(rootUri),
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Unmutes the specified list of accounts. Requires authentication.
        /// </summary>
        /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to unmute.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to remove the mutes from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> UnmuteActorList(
            AtUri listUri,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listUri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<EmptyResponse> response = await client.Post(
                service,
                $"{UnmuteActorListEndpoint}",
                new MuteActorListRequest(listUri),
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Unmutes the specified account. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to unmute</param>
        /// <param name="service">The <see cref="Uri"/> of the service remote the mute from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> UnmuteActor(
            AtIdentifier actor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<EmptyResponse> response = await client.Post(
                service,
                $"{UnmuteActorEndpoint}",
                new MuteActorRequest(actor),
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Unmutes a thread. Requires authentication.
        /// </summary>
        /// <param name="rootUri">The <see cref="AtUri"/> of the thread to unmute</param>
        /// <param name="service">The <see cref="Uri"/> of the service remove the mute from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootUri"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> UnmuteThread(
            AtUri rootUri,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rootUri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<EmptyResponse> response = await client.Post(
                service,
                $"{UnmuteThreadEndpoint}",
                new MuteThreadRequest(rootUri),
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Get a list of starter packs created by the <paramref name="actor"/>.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose starter packs should be returned.</param>
        /// <param name="limit">The maximum number of starter packs to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service remove the mute from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackViewBasic>>> GetActorStarterPacks(
            AtIdentifier actor,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }
            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetActorStarterPacksResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetActorStarterPacksResponse> response = await client.Get(
                service,
                $"{GetActorStarterPacksEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                PagedViewReadOnlyCollection<StarterPackViewBasic> pagedCollection =
                    new(
                        new List<StarterPackViewBasic>(response.Result.StarterPacks).AsReadOnly(),
                        response.Result.Cursor);
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackViewBasic>>(
                    pagedCollection,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackViewBasic>>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }
    }
}
