// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.Drafts.Model;

namespace idunno.Bluesky
{
    public partial class BlueskyServer
    {
        private const string CreateDraftEndpoint = "/xrpc/app.bsky.draft.createDraft";

        private const string DeleteDraftEndpoint = "/xrpc/app.bsky.draft.deleteDraft";

        private const string GetDraftsEndpoint = "/xrpc/app.bsky.draft.getDrafts";

        private const string UpdateDraftEndpoint = "/xrpc/app.bsky.draft.updateDraft";

        /// <summary>
        /// Creates a new draft for the authenticated user.
        /// </summary>
        /// <param name="draft">The <see cref="Draft"/> to create.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draft"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<TimestampIdentifier>> CreateDraft(
            Draft draft,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(draft);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<CreateDraftResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<CreateDraftResponse> result = await request.Post(
                service,
                CreateDraftEndpoint,
                record: new CreateDraftRequest(draft),
                requestHeaders: null,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                return new AtProtoHttpResult<TimestampIdentifier>(
                    new TimestampIdentifier(result.Result.Id),
                    result.StatusCode,
                    result.HttpResponseHeaders,
                    result.AtErrorDetail,
                    result.RateLimit);

            }
            else
            {
                return new AtProtoHttpResult<TimestampIdentifier>(
                    null,
                    result.StatusCode,
                    result.HttpResponseHeaders,
                    result.AtErrorDetail,
                    result.RateLimit);
            }
        }

        /// <summary>
        /// Deletes a draft by ID.
        /// </summary>
        /// <param name="draftId">The ID of the draft to delete.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftId"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> DeleteDraft(
            TimestampIdentifier draftId,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(draftId);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<EmptyResponse> result = await request.Post(
                service,
                record: new DeleteDraftRequest(draftId),
                endpoint: $"{DeleteDraftEndpoint}?id={draftId}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                return new AtProtoHttpResult<EmptyResponse>(
                    new EmptyResponse(),
                    result.StatusCode,
                    result.HttpResponseHeaders,
                    result.AtErrorDetail,
                    result.RateLimit);

            }
            else
            {
                return new AtProtoHttpResult<EmptyResponse>(
                    null,
                    result.StatusCode,
                    result.HttpResponseHeaders,
                    result.AtErrorDetail,
                    result.RateLimit);
            }
        }

        /// <summary>
        /// Get a paged list of suggested drafts for the authenticated user.
        /// </summary>
        /// <param name="limit">The maximum number of drafts to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<DraftView>>> GetDrafts(
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            int limitValue = limit ?? 50;

            ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
            ArgumentOutOfRangeException.ThrowIfZero(limitValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, Maximum.ListedDrafts);

            StringBuilder uriBuilder = new(GetDraftsEndpoint);

            if (cursor is not null || limit.HasValue)
            {
                uriBuilder.Append('?');
            }

            if (cursor is not null)
            {
                uriBuilder.Append(CultureInfo.InvariantCulture, $"cursor={cursor}");

                if (limit.HasValue)
                {
                    uriBuilder.Append('&');
                }
            }

            if (limit.HasValue)
            {
                uriBuilder.Append(CultureInfo.InvariantCulture, $"limit={limitValue}");
            }

            string endpoint = uriBuilder.ToString();

            AtProtoHttpClient<GetDraftsResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetDraftsResponse> response = await request.Get(
                service,
                endpoint,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<DraftView>>(
                    new PagedViewReadOnlyCollection<DraftView>(new List<DraftView>(response.Result.Drafts).AsReadOnly(), response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<DraftView>>(
                    new PagedViewReadOnlyCollection<DraftView>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Updates the draft for the authenticated user. If the draft ID points to a non-existing ID, the update will be silently ignored.
        /// </summary>
        /// <param name="draftWithId">The <see cref="DraftWithId"/> to create.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftWithId"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> UpdateDraft(
            DraftWithId draftWithId,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(draftWithId);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<CreateDraftResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<CreateDraftResponse> result = await request.Post(
                service,
                UpdateDraftEndpoint,
                record: new UpdateDraftRequest(draftWithId),
                requestHeaders: null,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                return new AtProtoHttpResult<EmptyResponse>(
                    new EmptyResponse(),
                    result.StatusCode,
                    result.HttpResponseHeaders,
                    result.AtErrorDetail,
                    result.RateLimit);

            }
            else
            {
                return new AtProtoHttpResult<EmptyResponse>(
                    null,
                    result.StatusCode,
                    result.HttpResponseHeaders,
                    result.AtErrorDetail,
                    result.RateLimit);
            }
        }
    }
}
