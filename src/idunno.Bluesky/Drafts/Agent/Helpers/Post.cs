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

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    private static readonly TimeSpan s_videoUploadPollingInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Creates a Bluesky post record from the specified <paramref name="draftWithId"/>.
    /// </summary>
    /// <param name="draftWithId">The <see cref="DraftWithId"/> to use to create the post record(s).</param>
    /// <param name="extractFacets">Automatically extracts rich text facets from the draft post content.</param>
    /// <param name="deleteDraft">Flag indicating whether to delete the saved draft if posting it is successful.</param>
    /// <param name="interactionPreferences">The current user's interaction preferences, if any.</param>
    /// <param name="mediaPathValidation">Specifies how the local media paths in <paramref name="draftWithId"/> are validated before the files they point to are read and uploaded.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftWithId"/> or its Draft property is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="DraftException">Thrown when <paramref name="draftWithId"/> cannot be converted to a post, or when one of its local media paths is rejected.</exception>
    [SuppressMessage("Minor Code Smell", "S1199:Nested code blocks should not be used", Justification = "Nesting is due to a logger scope.")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "An overload for cancellationToken is a standard api")]
    public async Task<AtProtoHttpResult<IReadOnlyList<CreateRecordResult>>> Post(
        DraftWithId draftWithId,
        bool extractFacets,
        bool deleteDraft,
        PostInteractionSettingsPreferences? interactionPreferences,
        DraftMediaPathValidation mediaPathValidation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draftWithId);
        ArgumentNullException.ThrowIfNull(draftWithId.Draft);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        using (_logger.BeginScope($"Posting Draft ID {draftWithId.Id}"))
        {
            List<CreateRecordResult> results = [];
            StrongReference? rootPostStrongReference = null;
            StrongReference? previousPostStrongReference = null;
            AtProtoHttpResult<CreateRecordResult>? postResult = null;

            int videoCount = 0;
            long totalVideoUploadSize = 0;

            int validationOffset = -1;

            // First we check that any local media exists
            foreach (DraftPost? draftPost in draftWithId.Draft.Posts)
            {
                validationOffset++;

                if (draftPost is null)
                {
                    continue;
                }

                if (draftPost.Text.GetUtf8Length() > Maximum.PostLengthInBytes || draftPost.Text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
                {
                    throw new DraftException($"Draft text in DraftPost[{validationOffset}] is too long for a real post.");
                }

                if ((draftPost.EmbedImages is not null && draftPost.EmbedGallery is not null) ||
                    (draftPost.EmbedImages is not null && draftPost.EmbedVideos is not null) ||
                    (draftPost.EmbedGallery is not null && draftPost.EmbedVideos is not null))
                {
                    throw new DraftException($"DraftPost[{validationOffset}] has more than one type of media embedded.");
                }

                if (draftPost.EmbedImages is not null)
                {
                    foreach (string mediaPath in draftPost.EmbedImages.Select(image => image.LocalRef.Path))
                    {
                        string resolvedImagePath = ResolveDraftMediaPath(mediaPath, validationOffset, draftWithId.Id, mediaPathValidation);

                        if (!File.Exists(resolvedImagePath))
                        {
                            throw new DraftException($"Embedded image {mediaPath} in DraftPost[{validationOffset}] not found.");
                        }
                    }
                }

                if (draftPost.EmbedGallery is not null && draftPost.EmbedGallery.Items is not null)
                {
                    foreach (string mediaPath in draftPost.EmbedGallery.Items.Select(image => image.LocalRef.Path))
                    {
                        string resolvedImagePath = ResolveDraftMediaPath(mediaPath, validationOffset, draftWithId.Id, mediaPathValidation);

                        if (!File.Exists(resolvedImagePath))
                        {
                            throw new DraftException($"Embedded Gallary has embedded image {mediaPath} in DraftPost[{validationOffset}] not found.");
                        }
                    }
                }

                if (draftPost.EmbedVideos is not null)
                {
                    foreach (string mediaPath in draftPost.EmbedVideos.Select(video => video.LocalRef.Path))
                    {
                        string resolvedVideoPath = ResolveDraftMediaPath(mediaPath, validationOffset, draftWithId.Id, mediaPathValidation);

                        if (!File.Exists(resolvedVideoPath))
                        {
                            throw new DraftException($"Embedded video {mediaPath} in DraftPost[{validationOffset}] not found.");
                        }

                        videoCount++;
                        totalVideoUploadSize += new FileInfo(resolvedVideoPath).Length;
                    }
                }
            }

            // Now check the upload quote for videos if we have any.
            if (videoCount > 0)
            {
                AtProtoHttpResult<UploadLimits> videoUploadLimitsResult = await GetUploadLimits(cancellationToken: cancellationToken).ConfigureAwait(false);
                videoUploadLimitsResult.EnsureSucceeded();

                if (!videoUploadLimitsResult.Result.CanUpload ||
                    (videoUploadLimitsResult.Result.RemainingDailyVideos is not null && videoUploadLimitsResult.Result.RemainingDailyVideos < videoCount) ||
                    (videoUploadLimitsResult.Result.RemainingDailyBytes is not null && videoUploadLimitsResult.Result.RemainingDailyBytes < totalVideoUploadSize))
                {
                    throw new DraftException($"Video upload limits exceeded for all the videos in the draft.");
                }
            }

            // Now we go through each post in the draft, so we can build a thread if needed.
            bool firstPost = true;
            int postOffset = -1;
            foreach (DraftPost? draftPost in draftWithId.Draft.Posts)
            {
                postOffset++;

                if (draftPost is null)
                {
                    continue;
                }

                PostBuilder postBuilder = new()
                {
                    Text = draftPost.Text,
                    Langs = draftWithId.Draft.Langs,
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

                if (draftPost.EmbedExternals is not null && draftPost.EmbedExternals.Count != 0)
                {
                    // The lexicon models an external embed as a bare uri, so there is no title or description to
                    // carry over. They are left empty rather than being filled with the uri.
                    EmbeddedExternal embeddedExternal = new(
                        uri: draftPost.EmbedExternals[0].Uri.ToString(),
                        title: string.Empty,
                        description: string.Empty,
                        thumbnail: null);
                    postBuilder.Embed = embeddedExternal;
                }

                if (draftPost.EmbedRecords is not null && draftPost.EmbedRecords.Count != 0)
                {
                    EmbeddedRecord embeddedRecord = new(draftPost.EmbedRecords[0].Record);
                    postBuilder.EmbedRecord(embeddedRecord);
                }

                // Upload the images for the post, then attach.
                foreach (DraftEmbedImage embed in draftPost.EmbedImages ?? [])
                {
                    string resolvedImagePath = ResolveDraftMediaPath(embed.LocalRef.Path, postOffset, draftWithId.Id, mediaPathValidation);

                    Logger.UploadingImageFromDraft(_logger, embed.LocalRef.Path, draftWithId.Id);

                    string? mimeType = MapExtensionToMimeType(resolvedImagePath) ?? throw new DraftException($"Unsupported image format for file {embed.LocalRef.Path}.");

                    byte[] fileBytes = await File.ReadAllBytesAsync(resolvedImagePath, cancellationToken: cancellationToken).ConfigureAwait(false);
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

                // Upload the gallery images for the post, then attach.
                if (draftPost.EmbedGallery is not null)
                {
                    foreach (DraftEmbedImage embed in draftPost.EmbedGallery.Items ?? [])
                    {
                        string resolvedImagePath = ResolveDraftMediaPath(embed.LocalRef.Path, postOffset, draftWithId.Id, mediaPathValidation);

                        Logger.UploadingGalleryImageFromDraft(_logger, embed.LocalRef.Path, draftWithId.Id);
                        string? mimeType = MapExtensionToMimeType(resolvedImagePath) ?? throw new DraftException($"Unsupported image format for file {embed.LocalRef.Path}.");
                        byte[] fileBytes = await File.ReadAllBytesAsync(resolvedImagePath, cancellationToken: cancellationToken).ConfigureAwait(false);
                        AtProtoHttpResult<EmbeddedImage> uploadResult = await UploadImage(
                            fileBytes,
                            mimeType: mimeType,
                            altText: embed.AltText ?? string.Empty,
                            aspectRatio: null,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (!uploadResult.Succeeded)
                        {
                            throw new DraftException($"Failed to upload gallery image {embed.LocalRef.Path}: {uploadResult.StatusCode} {uploadResult.AtErrorDetail?.Error} {uploadResult.AtErrorDetail?.Message}");
                        }
                        postBuilder.Add(uploadResult.Result);
                    }
                }

                // Upload the videos for the post, with their captions if any, then attach.
                if (draftPost.EmbedVideos is not null)
                {
                    foreach (DraftEmbedVideo embedVideo in draftPost.EmbedVideos)
                    {
                        string path = embedVideo.LocalRef.Path;
                        string resolvedVideoPath = ResolveDraftMediaPath(path, postOffset, draftWithId.Id, mediaPathValidation);

                        Logger.UploadingVideoFromDraft(_logger, path, draftWithId.Id);

                        byte[] fileBytes = await File.ReadAllBytesAsync(resolvedVideoPath, cancellationToken: cancellationToken).ConfigureAwait(false);

                        AtProtoHttpResult<UploadLimits> videoUploadLimitsResult = await GetUploadLimits(cancellationToken: cancellationToken).ConfigureAwait(false);
                        videoUploadLimitsResult.EnsureSucceeded();

                        if (!videoUploadLimitsResult.Result.CanUpload ||
                            (videoUploadLimitsResult.Result.RemainingDailyVideos is not null && videoUploadLimitsResult.Result.RemainingDailyVideos == 0) ||
                            (videoUploadLimitsResult.Result.RemainingDailyBytes is not null && videoUploadLimitsResult.Result.RemainingDailyBytes < fileBytes.LongLength))
                        {
                            throw new DraftException($"Video upload limits exceeded. Cannot upload video {path}");
                        }

                        AtProtoHttpResult<JobStatus> uploadResult = await UploadVideo(
                            Path.GetFileName(resolvedVideoPath),
                            fileBytes,
                            MapExtensionToVideoMimeType(resolvedVideoPath),
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (!uploadResult.Succeeded)
                        {
                            throw new DraftException($"Failed to upload video {path}: {uploadResult.StatusCode} {uploadResult.AtErrorDetail?.Error} {uploadResult.AtErrorDetail?.Message}");
                        }

                        while (uploadResult.Succeeded &&
                            (uploadResult.Result.State == JobState.Created || uploadResult.Result.State == JobState.InProgress))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            await Task.Delay(s_videoUploadPollingInterval, cancellationToken: cancellationToken).ConfigureAwait(false);
                            uploadResult = await GetJobStatus(uploadResult.Result.JobId, cancellationToken: cancellationToken).ConfigureAwait(false);
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

                            postBuilder.Add(new EmbeddedVideo(uploadResult.Result.Blob, captions: captions, altText: embedVideo.AltText));
                        }
                        else
                        {
                            postBuilder.Add(new EmbeddedVideo(uploadResult.Result.Blob, altText: embedVideo.AltText));
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftWithId"/> or its Draft property is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="DraftException">Thrown when <paramref name="draftWithId"/> cannot be converted to a post, or when one of its local media paths is rejected.</exception>
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
            mediaPathValidation: DraftMediaPathValidation.Enforce,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a Bluesky post record from the specified <paramref name="draftWithId"/> and deletes the draft if it is successfully posted.
    /// </summary>
    /// <param name="draftWithId">The <see cref="DraftWithId"/> to use to create the post record(s).</param>
    /// <param name="mediaPathValidation">Specifies how the local media paths in <paramref name="draftWithId"/> are validated before the files they point to are read and uploaded.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftWithId"/> or its Draft property is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="DraftException">Thrown when <paramref name="draftWithId"/> cannot be converted to a post, or when one of its local media paths is rejected.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "An overload for cancellationToken is a standard api")]
    public async Task<AtProtoHttpResult<IReadOnlyList<CreateRecordResult>>> Post(
        DraftWithId draftWithId,
        DraftMediaPathValidation mediaPathValidation,
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
            mediaPathValidation: mediaPathValidation,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates a local media path taken from a draft and returns the path that should be used to read the file it refers to.
    /// </summary>
    private string ResolveDraftMediaPath(
        string path,
        int postIndex,
        TimestampIdentifier draftId,
        DraftMediaPathValidation mediaPathValidation)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw RejectDraftMediaPath(path, postIndex, draftId, "the path is empty");
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw RejectDraftMediaPath(path, postIndex, draftId, "the path contains characters which are not valid in a path");
        }

        if (mediaPathValidation == DraftMediaPathValidation.Trust)
        {
            return path;
        }

        if (DraftMediaRoots.Count == 0)
        {
            throw RejectDraftMediaPath(
                path,
                postIndex,
                draftId,
                $"no draft media roots are configured. Set {nameof(BlueskyAgentOptions)}.{nameof(BlueskyAgentOptions.DraftMediaRoots)}, or post the draft with {nameof(DraftMediaPathValidation)}.{nameof(DraftMediaPathValidation.Trust)} if its paths are known to be correct");
        }

        // A UNC or device path is fully qualified, but reading one reaches out to another host or to a raw device,
        // so neither is ever an acceptable source for draft media.
        if (IsUncOrDevicePath(path))
        {
            throw RejectDraftMediaPath(path, postIndex, draftId, "UNC and device paths are not allowed");
        }

        // A relative path would be resolved against the current working directory, which is never what a device bound draft means.
        if (!Path.IsPathFullyQualified(path))
        {
            throw RejectDraftMediaPath(path, postIndex, draftId, "the path is not fully qualified");
        }

        string resolvedPath;

        try
        {
            resolvedPath = Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            throw RejectDraftMediaPath(path, postIndex, draftId, "the path could not be resolved");
        }
        catch (PathTooLongException)
        {
            throw RejectDraftMediaPath(path, postIndex, draftId, "the path is too long to resolve");
        }

        if (!IsWithinDraftMediaRoots(resolvedPath))
        {
            throw RejectDraftMediaPath(path, postIndex, draftId, "the path resolves outside every configured draft media root");
        }

        // A link inside a permitted root can still point outside it, so the final target is checked as well.
        string? linkTargetPath = ResolveFinalLinkTarget(resolvedPath);

        if (linkTargetPath is not null && !IsWithinDraftMediaRoots(linkTargetPath))
        {
            throw RejectDraftMediaPath(path, postIndex, draftId, "the path resolves, through a link, outside every configured draft media root");
        }

        return resolvedPath;
    }

    private DraftException RejectDraftMediaPath(string path, int postIndex, TimestampIdentifier draftId, string reason)
    {
        Logger.DraftMediaPathRejected(_logger, path, draftId, reason);

        return new DraftException($"Embedded media path \"{path}\" in DraftPost[{postIndex}] was rejected because {reason}.");
    }

    private bool IsWithinDraftMediaRoots(string candidatePath)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // The separator is appended so that a root of "/media" does not match a sibling directory named "/mediaElsewhere".
        return DraftMediaRoots.Any(root => candidatePath.StartsWith(
            root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar,
            comparison));
    }

    private static bool IsUncOrDevicePath(string path)
    {
        return path.Length >= 2 &&
            (path[0] == '\\' || path[0] == '/') &&
            (path[1] == '\\' || path[1] == '/');
    }

    private static string? ResolveFinalLinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
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

    private static string MapExtensionToVideoMimeType(string file)
    {
        if (file.EndsWith(".mov", StringComparison.OrdinalIgnoreCase))
        {
            return "video/quicktime";
        }
        else if (file.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
        {
            return "video/webm";
        }
        else if (file.EndsWith(".mpg", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".mpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "video/mpeg";
        }
        else
        {
            return "video/mp4";
        }
    }
}