// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Video;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Creates a draft for the authenticated user.
        /// </summary>
        /// <param name="draft">The draft to create.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="draft"/> is null.</exception>
        public async Task<AtProtoHttpResult<TimestampIdentifier>> CreateDraft(
            Draft draft,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(draft);

            return await BlueskyServer.CreateDraft(
                draft,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a draft by ID.
        /// </summary>
        /// <param name="draftId">The ID of the draft to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftId"/> is null.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> DeleteDraft(
            TimestampIdentifier draftId,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(draftId);

            return await BlueskyServer.DeleteDraft(
                draftId,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paged list of suggested drafts for the authenticated user.
        /// </summary>
        /// <param name="limit">The maximum number of drafts to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is negative, zero, or greater than <see cref="Maximum.ListedDrafts"/>.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<DraftView>>> GetDrafts(
            int? limit = null,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(limit.Value);
                ArgumentOutOfRangeException.ThrowIfZero(limit.Value);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.ListedDrafts);
            }

            return await BlueskyServer.GetDrafts(
                limit,
                cursor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a draft for the authenticated user.
        /// </summary>
        /// <param name="draftWithId">The draft and ID to update.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftWithId"/> is null.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UpdateDraft(
            DraftWithId draftWithId,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(draftWithId);

            return await BlueskyServer.UpdateDraft(
                draftWithId,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record from the specified <paramref name="draftWithId"/>.
        /// </summary>
        /// <param name="draftWithId">The <see cref="DraftWithId"/> to use to create the post record(s).</param>
        /// <param name="extractFacets">Automatically extracts rich text facets from the draft post content.</param>
        /// <param name="deleteDraft">Flag indicating whether to delete the saved draft if posting it is successful.</param>
        /// <param name="interactionPreferences">The current user's interaction preferences, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftWithId"/> or its Draft property is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="DraftException">Thrown when <paramref name="draftWithId"/> cannot be converted to a post.</exception>
        [SuppressMessage("Minor Code Smell", "S1199:Nested code blocks should not be used", Justification = "Nesting is due to a logger scope.")]
        [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "An overload for cancellationToken is a standard api")]
        public async Task<AtProtoHttpResult<IReadOnlyList<CreateRecordResult>>> Post(
            DraftWithId draftWithId,
            bool extractFacets,
            bool deleteDraft,
            InteractionPreferences? interactionPreferences,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(draftWithId);
            ArgumentNullException.ThrowIfNull(draftWithId.Draft);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            _logger.BeginScope($"Posting Draft ID {draftWithId.Id}");
            {
                List<CreateRecordResult> results = [];
                StrongReference? rootPostStrongReference = null;
                StrongReference? previousPostStrongReference = null;
                AtProtoHttpResult<CreateRecordResult>? postResult = null;

                // First we check that any local media exists
                foreach (DraftPost? draftPost in draftWithId.Draft.Posts)
                {
                    if (draftPost is null)
                    {
                        continue;
                    }

                    if (draftPost.EmbedImages is not null)
                    {
                        var missingImages = draftPost.EmbedImages
                            .Where(image => !File.Exists(image.LocalRef.Path))
                            .ToList();

                        if (missingImages.Count > 0)
                        {
                            throw new DraftException($"Embedded image {missingImages[0].LocalRef.Path} not found.");
                        }
                    }

                    if (draftPost.EmbedVideos is not null)
                    {
                        var missingVideos = draftPost.EmbedVideos
                            .Select(embed => embed.LocalRef)
                            .Where(localRef => !File.Exists(localRef.Path))
                            .ToList();

                        if (missingVideos.Count > 0)
                        {
                            throw new DraftException($"Embedded video {missingVideos[0].Path} not found.");
                        }
                    }
                }

                // Now we go through each post in the draft, so we can build a thread if needed.
                bool firstPost = true;
                foreach (DraftPost? draftPost in draftWithId.Draft.Posts)
                {
                    if (draftPost is null)
                    {
                        continue;
                    }

                    PostBuilder postBuilder = new()
                    {
                        Text = draftPost.Text,
                        Langs = draftWithId.Draft!.Langs,
                    };

                    if (extractFacets)
                    {
                        await postBuilder.ExtractFacets(FacetExtractor, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }

                    if (draftPost.Labels is not null)
                    {
                        postBuilder.SetSelfLabels(new PostSelfLabels(draftPost.Labels));
                    }

                    if (firstPost)
                    {
                        if (draftWithId.Draft.ThreadGateAllowRules is not null)
                        {
                            postBuilder.ThreadGateRules = [.. draftWithId.Draft.ThreadGateAllowRules];
                        }
                        else if (interactionPreferences?.ThreadGateAllowRules is not null)
                        {
                            postBuilder.ThreadGateRules = [.. interactionPreferences.ThreadGateAllowRules];
                        }
                    }
                    else
                    {
                        ReplyReferences replyReferences = new(
                            root: rootPostStrongReference ?? throw new DraftException("rootPostStrongReference is unexpectedly null."),
                            parent: previousPostStrongReference ?? throw new DraftException("previousPostStrongReference is unexpectedly null.")
                        );
                        postBuilder.InReplyTo = replyReferences;
                    }

                    if (draftWithId.Draft.PostGateEmbeddingRules is not null)
                    {
                        postBuilder.PostGateRules = [.. draftWithId.Draft.PostGateEmbeddingRules];
                    }
                    else if (interactionPreferences?.PostGateEmbeddingRules is not null)
                    {
                        postBuilder.PostGateRules = [.. interactionPreferences.PostGateEmbeddingRules];
                    }

                    if (draftPost.EmbedExternals is not null && draftPost.EmbedExternals[0] is not null)
                    {
                        EmbeddedExternal embeddedExternal = new(
                            uri: draftPost.EmbedExternals[0].Uri,
                            title: draftPost.EmbedExternals[0].Uri.ToString());
                        postBuilder.Embed = embeddedExternal;
                    }

                    if (draftPost.EmbedRecords is not null && draftPost.EmbedRecords[0] is not null)
                    {
                        EmbeddedRecord embeddedRecord = new(draftPost.EmbedRecords[0].Record);
                        postBuilder.EmbedRecord(embeddedRecord);
                    }

                    // Upload the images for the post, then attach.
                    foreach (DraftEmbedImage embed in draftPost.EmbedImages ?? [])
                    {
                        Logger.UploadingImageFromDraft(_logger, embed.LocalRef.Path, draftWithId.Id);

                        string? mimeType = MapExtensionToMimeType(embed.LocalRef.Path) ?? throw new DraftException($"Unsupported image format for file {embed.LocalRef.Path}.");

                        byte[] fileBytes = await File.ReadAllBytesAsync(embed.LocalRef.Path, cancellationToken: cancellationToken).ConfigureAwait(false);
                        AtProtoHttpResult<EmbeddedImage> uploadResult = await UploadImage(
                            fileBytes,
                            mimeType: mimeType,
                            altText: embed.AltText ?? string.Empty,
                            aspectRatio: null,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (!uploadResult.Succeeded)
                        {
                            throw new DraftException($"Failed to upload image {embed.LocalRef.Path}: {uploadResult.StatusCode} {uploadResult.AtErrorDetail?.Error} {uploadResult.AtErrorDetail?.Message}");
                        }

                        postBuilder.Add(uploadResult.Result);
                    }

                    // Upload the videos for the post, with their captions if any, then attach.
                    if (draftPost.EmbedVideos is not null)
                    {
                        foreach (DraftEmbedVideo embedVideo in draftPost.EmbedVideos)
                        {
                            string path = embedVideo.LocalRef.Path;

                            Logger.UploadingVideoFromDraft(_logger, path, draftWithId.Id);

                            byte[] fileBytes = await File.ReadAllBytesAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);

                            AtProtoHttpResult<UploadLimits> videoUploadLimitsResult = await GetVideoUploadLimits(cancellationToken: cancellationToken).ConfigureAwait(false);
                            videoUploadLimitsResult.EnsureSucceeded();

                            if (!videoUploadLimitsResult.Result.CanUpload ||
                                videoUploadLimitsResult.Result.RemainingDailyVideos == 0 ||
                                videoUploadLimitsResult.Result.RemainingDailyBytes < (ulong)fileBytes.LongLength)
                            {
                                throw new DraftException($"Video upload limits exceeded. Cannot upload video {path}");
                            }

                            AtProtoHttpResult<JobStatus> uploadResult = await UploadVideo(
                                Path.GetFileName(path),
                                fileBytes,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (!uploadResult.Succeeded)
                            {
                                throw new DraftException($"Failed to upload video {path}: {uploadResult.StatusCode} {uploadResult.AtErrorDetail?.Error} {uploadResult.AtErrorDetail?.Message}");
                            }

                            while (uploadResult.Succeeded &&
                                (uploadResult.Result.State == JobState.Created || uploadResult.Result.State == JobState.InProgress) &&
                                !cancellationToken.IsCancellationRequested)
                            {
                                await Task.Delay(1000, cancellationToken: cancellationToken).ConfigureAwait(false);
                                uploadResult = await GetVideoJobStatus(uploadResult.Result.JobId, cancellationToken: cancellationToken).ConfigureAwait(false);
                            }

                            if (!uploadResult.Succeeded || uploadResult.Result.Blob is null || uploadResult.Result.State != JobState.Completed)
                            {
                                throw new DraftException(
                                    $"Video upload for {path} failed {uploadResult.Result?.State} {uploadResult.StatusCode} {uploadResult.AtErrorDetail?.Error} {uploadResult.AtErrorDetail?.Message}");
                            }

                            if (embedVideo.Captions is not null)
                            {
                                List<Caption> captions = [];
                                foreach (DraftEmbedCaption caption in embedVideo.Captions)
                                {
                                    byte[] captionsBytes = System.Text.Encoding.UTF8.GetBytes(caption.Content);

                                    string fileName;

                                    if (caption.Lang is not null)
                                    {
                                        fileName = Path.GetFileNameWithoutExtension(path) + $"_{caption.Lang}.vtt";
                                    }
                                    else
                                    {
                                        fileName = Path.GetFileNameWithoutExtension(path) + $".vtt";
                                    }

                                    AtProtoHttpResult<Caption> captionsUploadResult = await UploadCaptions(
                                        captionsBytes,
                                        fileName,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

                                    if (!captionsUploadResult.Succeeded)
                                    {
                                        throw new DraftException($"Failed to upload captions for video {path}: {captionsUploadResult.StatusCode} {captionsUploadResult.AtErrorDetail?.Error} {captionsUploadResult.AtErrorDetail?.Message}");
                                    }

                                    captions.Add(captionsUploadResult.Result);
                                }

                                postBuilder.Add(new EmbeddedVideo(uploadResult.Result.Blob, captions));
                            }
                            else
                            {
                                postBuilder.Add(new EmbeddedVideo(uploadResult.Result.Blob));
                            }
                        }
                    }

                    postResult = await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!postResult.Succeeded)
                    {
                        return new AtProtoHttpResult<IReadOnlyList<CreateRecordResult>>(
                            result: null,
                            statusCode: postResult.StatusCode,
                            atErrorDetail: postResult.AtErrorDetail,
                            httpResponseHeaders: postResult.HttpResponseHeaders,
                            rateLimit: postResult.RateLimit
                        );
                    }

                    if (firstPost)
                    {
                        rootPostStrongReference = postResult.Result.StrongReference;
                        previousPostStrongReference = postResult.Result.StrongReference;
                        firstPost = false;
                    }
                    else
                    {
                        previousPostStrongReference = postResult.Result.StrongReference;
                    }

                    results.Add(postResult.Result);
                }

                if (deleteDraft)
                {
                    AtProtoHttpResult<EmptyResponse> deleteDraftResult = await DeleteDraft(draftWithId.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (!deleteDraftResult.Succeeded)
                    {
                        Logger.DeleteDraftFailed(_logger, draftWithId.Id, deleteDraftResult.StatusCode, deleteDraftResult.AtErrorDetail?.Error, deleteDraftResult.AtErrorDetail?.Message);
                    }
                }

                return new AtProtoHttpResult<IReadOnlyList<CreateRecordResult>>(
                    results,
                    statusCode: postResult?.StatusCode ?? HttpStatusCode.NoContent,
                    httpResponseHeaders: postResult?.HttpResponseHeaders,
                    atErrorDetail: postResult?.AtErrorDetail,
                    rateLimit: postResult?.RateLimit);
            }
        }

        /// <summary>
        /// Creates a Bluesky post record from the specified <paramref name="draftWithId"/> and deletes the draft if it is successfully posted.
        /// </summary>
        /// <param name="draftWithId">The <see cref="DraftWithId"/> to use to create the post record(s).</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftWithId"/> or its Draft property is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "An overload for cancellationToken is a standard api")]
        public async Task<AtProtoHttpResult<IReadOnlyList<CreateRecordResult>>> Post(
            DraftWithId draftWithId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(draftWithId);
            ArgumentNullException.ThrowIfNull(draftWithId.Draft);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await Post(
                draftWithId,
                extractFacets: true,
                deleteDraft: true,
                interactionPreferences: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private static string? MapExtensionToMimeType(string file)
        {
            if (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return "image/jpeg";
            }
            else if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return "image/png";
            }
            else if (file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                return "image/gif";
            }
            else if (file.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return "image/webp";
            }
            else if (file.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                return "image/bmp";
            }
            else if (file.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".tif", StringComparison.OrdinalIgnoreCase))
            {
                return "image/tiff";
            }
            else if (file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return "image/svg+xml";
            }
            else if (file.EndsWith(".avif", StringComparison.OrdinalIgnoreCase))
            {
                return "image/avif";
            }
            else if (file.EndsWith(".heic", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".heif", StringComparison.OrdinalIgnoreCase))
            {
                return "image/heic";
            }
            else
            {
                return null;
            }
        }
    }
}
