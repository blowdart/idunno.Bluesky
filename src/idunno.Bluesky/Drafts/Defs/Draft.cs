// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Feed.Gates;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a draft containing an array of draft posts
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(Draft), typeDiscriminator: "app.bsky.draft.defs#draft")]
public record Draft
{
    /// <summary>
    /// Creates a new instance of <see cref="Draft"/> with the specified properties.
    /// </summary>
    /// <param name="posts">A list of posts for the draft. Maximum <see cref="Maximum.DraftPosts"/>.</param>
    /// <param name="deviceId">Identifier of the device that created this draft.</param>
    /// <param name="deviceName">The device and/or platform on which the draft was created. Maximum <see cref="Maximum.DraftDeviceNameLengthInBytes"/> UTF-8 bytes.</param>
    /// <param name="langs">A collection of RFC5646 language codes for the draft. Maximum <see cref="Maximum.DraftLangs"/>.</param>
    /// <param name="postGateEmbeddingRules">An collection of embedding rules for the draft posts. Maximum <see cref="Maximum.DraftPostGateEmbeddingRules"/>.</param>
    /// <param name="threadGateAllowRules">A collection of thread gate rules for the draft. Maximum <see cref="Maximum.DraftThreadGateAllowRules"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="posts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any of the collections, or <see cref="DeviceName"/> exceed their maximum allowed size.</exception>
    [JsonConstructor]
    public Draft(
        IReadOnlyList<DraftPost> posts,
        string? deviceId,
        string? deviceName = null,
        IReadOnlyList<string>? langs = null,
        IReadOnlyList<PostGateRule>? postGateEmbeddingRules = null,
        IReadOnlyList<ThreadGateRule>? threadGateAllowRules = null)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            deviceId?.GetUtf8Length() ?? 0,
            Maximum.DraftDeviceIdLengthInBytes);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            deviceName?.GetUtf8Length() ?? 0,
            Maximum.DraftDeviceNameLengthInBytes);


        // The lexicon does not declare a minimum length for any of the optional collections, so an empty collection is accepted.
        if (langs is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                langs.Count,
                Maximum.DraftLangs);
        }

        if (postGateEmbeddingRules is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                postGateEmbeddingRules.Count,
                Maximum.DraftPostGateEmbeddingRules);
        }

        if (threadGateAllowRules is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                threadGateAllowRules.Count,
                Maximum.DraftThreadGateAllowRules);
        }

        Posts = posts;
        DeviceId = deviceId;
        DeviceName = deviceName;
        Langs = langs is null ? null : new List<string>(langs).AsReadOnly();
        PostGateEmbeddingRules = postGateEmbeddingRules is null ? null : new List<PostGateRule>(postGateEmbeddingRules).AsReadOnly();
        ThreadGateAllowRules = threadGateAllowRules is null ? null : new List<ThreadGateRule>(threadGateAllowRules).AsReadOnly();
    }

    /// <summary>
    /// Creates a new instance of <see cref="Draft"/> with the specified properties.
    /// </summary>
    /// <param name="post">The draft post.</param>
    /// <param name="deviceId">Identifier of the device that created this draft.</param>
    /// <param name="deviceName">The device and/or platform on which the draft was created. Maximum <see cref="Maximum.DraftDeviceNameLengthInBytes"/> UTF-8 bytes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="deviceName"/> is specified and its length is greater than <see cref="Maximum.DraftDeviceNameLengthInBytes"/> UTF-8 bytes.</exception>
    public Draft(
        DraftPost post,
        string? deviceId,
        string? deviceName) : this(
            posts: [post],
            deviceId: deviceId,
            deviceName: deviceName,
            langs: null,
            postGateEmbeddingRules: null,
            threadGateAllowRules: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Draft"/> with the specified properties.
    /// </summary>
    /// <param name="text">The text to create a draft post from.</param>
    /// <param name="deviceId">Identifier of the device that created this draft.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/> or whitespace.</exception>
    public Draft(
        string text,
        string? deviceId) : this(
            posts: [new DraftPost(text)],
            deviceId: deviceId,
            deviceName: null,
            langs: null,
            postGateEmbeddingRules: null,
            threadGateAllowRules: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Draft"/> with the specified properties.
    /// </summary>
    /// <param name="text">The text to create a draft post from.</param>
    /// <param name="deviceId">Identifier of the device that created this draft.</param>
    /// <param name="deviceName">The device and/or platform on which the draft was created. Maximum <see cref="Maximum.DraftDeviceNameLengthInBytes"/> UTF-8 bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="deviceName"/> is specified and its length is greater than <see cref="Maximum.DraftDeviceNameLengthInBytes"/> UTF-8 bytes.</exception>"
    public Draft(
        string text,
        string? deviceId,
        string? deviceName) : this(
            posts: [new DraftPost(text)],
            deviceId: deviceId,
            deviceName: deviceName,
            langs: null,
            postGateEmbeddingRules: null,
            threadGateAllowRules: null)
    {
    }

    /// <summary>
    /// Gets the identifier of the device that created this draft.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceId { get; }

    /// <summary>
    /// Gets the device and/or platform on which the draft was created. Maximum <see cref="Maximum.DraftDeviceNameLengthInBytes"/> UTF-8 bytes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceName { get; }

    /// <summary>
    /// Gets the array of draft posts that compose this draft.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when setting to an empty collection or one larger than <see cref="Maximum.DraftPosts"/>.</exception>
    [JsonRequired]
    public IReadOnlyList<DraftPost> Posts
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfLessThan(
                value.Count,
                1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                value.Count,
                Maximum.DraftPosts);

            field = new List<DraftPost>(value).AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the collection of language strings, if any, that the post is written in.
    /// </summary>
    /// <remarks>
    ///<para>A maximum of <see cref="Maximum.DraftLangs"/> languages can be specified. Languages should be specified in RFC5646 format.</para>
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Langs { get; }

    /// <summary>
    /// Gets the rules for the post gate to be created when this draft is published.
    /// </summary>
    /// <remarks><para>A maximum of <see cref="Maximum.DraftPostGateEmbeddingRules"/> post gates can be specified.</para></remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("postgateEmbeddingRules")]
    public IReadOnlyList<PostGateRule>? PostGateEmbeddingRules { get; }

    /// <summary>
    /// Gets the rules for the thread gates to be created when this draft is published.
    /// </summary>
    /// <remarks><para>A maximum of <see cref="Maximum.DraftThreadGateAllowRules"/> thread gates can be specified.</para></remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("threadgateAllow")]
    public IReadOnlyList<ThreadGateRule>? ThreadGateAllowRules { get; }
}