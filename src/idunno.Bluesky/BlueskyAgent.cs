// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Notifications;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from a Bluesky service.
    /// </summary>
    public partial class BlueskyAgent : AtProtoAgent
    {
        private readonly ILogger<BlueskyAgent> _logger;

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/>.
        /// </summary>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use when making requests.</param>
        /// <param name="loggerFactory">The logger factory to use for logging messages, if any.</param>
        /// <param name="options"><see cref="BlueskyAgentOptions"/> for the use in the creation of this instance of <see cref="BlueskyAgent"/>.</param>
        public BlueskyAgent(HttpClient? httpClient = null, ILoggerFactory? loggerFactory = default, BlueskyAgentOptions ? options = null) :
            base (DefaultServiceUris.BlueskyApiUri, httpClient, loggerFactory, options)
        {
            if (options is not null && options.PublicAppViewUri is not null)
            {
                ReadOnlyServiceUri = options.PublicAppViewUri;
            }

            loggerFactory ??= NullLoggerFactory.Instance;

            _logger = loggerFactory.CreateLogger<BlueskyAgent>();
        }

        /// <summary>
        /// Gets the service uri used to issue read only commands against.
        /// </summary>
        /// <value>
        /// The service used to issue read only commands against.
        /// </value>
        protected Uri ReadOnlyServiceUri { get; init; } = DefaultServiceUris.PublicAppViewUri;

        /// <summary>
        /// Gets the service uri to issue commands against, based on whether the current session is authenticated.
        /// </summary>
        /// <value>The service uri to issue commands against, based on whether the current session is authenticated.</value>
        protected Uri AuthenticatedOrUnauthenticatedServiceUri
        {
            get
            {
                if (!IsAuthenticated)
                {
                    return ReadOnlyServiceUri;
                }

                return Service;
            }
        }

        /// <summary>
        /// Gets a count the number of unread notifications for the requesting account.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="AtProtoHttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public async Task<AtProtoHttpResult<int>> GetNotificationUnreadCount(DateTimeOffset? seenAt = null, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetNotificationUnreadCount(seenAt, Service, AccessToken!, HttpClient, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(IEnumerable<Did>? subscribedLabelers = null, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await ListNotifications(null, null, null, subscribedLabelers, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of notifications to return. If specified this should be &gt;=1 and &lt;= 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(
            int? limit = null,
            string? cursor = null,
            DateTimeOffset? seenAt = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.ListNotifications(limit, cursor, seenAt, Service, AccessToken!, HttpClient, subscribedLabelers, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the date and time notifications were last seen for the current user.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UpdateNotificationSeenAt(DateTimeOffset? seenAt = null, CancellationToken cancellationToken = default)
        {
            if (Session is null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            seenAt ??= DateTimeOffset.UtcNow;

            return await BlueskyServer.UpdateNotificationSeenAt((DateTimeOffset)seenAt, Service, AccessToken!, HttpClient, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates an AT URI from the specified Uri.
        /// This Uri is very dependent on the Bluesky web client and its format is subject to change.
        /// </summary>
        /// <param name="uri">A URI from the Bluesky web client.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An AT URI corresponding to the resource the Bluesky web client URI.</returns>
        /// <remarks>
        /// <para>This method makes outgoing web requests to resolve the handle in a Bluesky URI to a <see cref="AtProto.Did"/>.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> is in an unexpected format.</exception>
        public async Task<AtUri> BuildAtUriFromBlueskyWebUri(Uri uri, CancellationToken cancellationToken = default)
        {
            // Bluesky web client URIs should be in the format
            // https://bsky.app/profile/<handle>/post/<rkey>/

            ArgumentNullException.ThrowIfNull(uri);

            if (uri.Scheme != "https")
            {
                throw new ArgumentException("Scheme is not https.");
            }

            if (uri.HostNameType != UriHostNameType.Dns || uri.Host != "bsky.app")
            {
                throw new ArgumentException($"{uri.Host} is not a known Bluesky web host.");
            }

            string[] pathComponents = uri.AbsolutePath.Split('/');

            if (pathComponents.Length != 5)
            {
                throw new ArgumentException($"{uri.AbsolutePath} does not have four components.");
            }

            if (pathComponents[1] != "profile")
            {
                throw new ArgumentException($"{uri.AbsolutePath} is not a profile path.");
            }

            if (pathComponents[3] != "post")
            {
                throw new ArgumentException($"{uri.AbsolutePath} is not a post path.");
            }

            if (!Handle.TryParse(pathComponents[2], out Handle? handle))
            {
                throw new ArgumentException($"{pathComponents[2]} is not a valid handle.");
            }

            Did? did = await ResolveHandle(handle.ToString(), cancellationToken).ConfigureAwait(false) ?? throw new HandleResolutionException($"Handle resolution did not succeed.");
            string rkey = pathComponents[4];

            string rebuiltAtUri = $"at://{did}/app.bsky.feed.post/{rkey}";

            if (!AtUri.TryParse(rebuiltAtUri, out AtUri? atUri))
            {
                throw new ArgumentException($"AtUri could not be created from {rebuiltAtUri}.", nameof(uri));
            }
            else
            {
                return atUri;
            }
        }

        /// <summary>
        /// Generates an Bluesky Web URI from the specified <paramref name="atUri" />, if the AT URI is in the app.bsky.feed.post collection.
        /// This Uri is very dependent on the Bluesky web client and its format is subject to change.
        /// </summary>
        /// <param name="atUri">The <see cref="AtUri"/> to generate a web URI for.</param>
        /// <returns>A URI for the Bluesky Web Client which will display the record specified by the <paramref name="atUri"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="atUri"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="atUri"/> is in an unexpected format or collection.</exception>
        public static Uri BuildBlueskyPostUriFromAtUri(AtUri atUri)
        {
            ArgumentNullException.ThrowIfNull(atUri);

            if (atUri.Collection is null)
            {
                throw new ArgumentException($"{atUri} has no collection.", nameof(atUri));
            }

            if (atUri.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException($"{atUri} collection is not {CollectionNsid.Post}.", nameof(atUri));
            }

            if (atUri.Repo is null)
            {
                throw new ArgumentException($"{atUri} has no repo.", nameof(atUri));
            }

            if (atUri.RecordKey is null)
            {
                throw new ArgumentException($"{atUri} has no recordKey.", nameof(atUri));
            }

            return new Uri($"https://bsky.app/profile/{atUri.Repo}/post/{atUri.RecordKey}");
        }
    }
}
