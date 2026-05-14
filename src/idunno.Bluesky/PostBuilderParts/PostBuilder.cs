// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.RichText;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A builder to allow building of a Post record in a more friendly manner.
/// </summary>
public sealed partial class PostBuilder : IEquatable<PostBuilder>
{
#if NET9_0_OR_GREATER
    private readonly Lock _syncLock = new ();
#else
    private readonly object _syncLock = new ();
#endif
    private readonly Post _post;
    private readonly List<EmbeddedImage> _embeddedImages = [];
    private EmbeddedVideo? _embeddedVideo;
    private List<ThreadGateRule>? _threadGateRules;
    private List<PostGateRule>? _postGateRules;
    private bool _disableReplies;

    private static readonly CompositeFormat s_postTextExceedsMaxLengthValidationError = CompositeFormat.Parse(Properties.Resources.PostTextExceedsMaxLengthValidationError);
    private static readonly CompositeFormat s_postTextExceedsMaxLengthInGraphemesValidationError = CompositeFormat.Parse(Properties.Resources.PostTextExceedsMaxLengthInGraphemesValidationError);
    private static readonly CompositeFormat s_postTextExceedsMaxLength = CompositeFormat.Parse(Properties.Resources.PostTextExceedsMaxLengths);

    /// <summary>
    /// Creates a new instance of a <see cref="PostBuilder"/>.
    /// </summary>
    public PostBuilder()
    {
        _post = new Post();
    }

    /// <summary>
    /// Creates a new instance of a <see cref="PostBuilder"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="lang">The languages for the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="images">A collection of <see cref="EmbeddedImage"/>s to attach to the post, if any.</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s to attach to the post text, if any.</param>
    /// <param name="labels">Any self labels to apply to the post.</param>
    /// <param name="tags">Any tags to apply to the post.</param>
    public PostBuilder(
        string? text,
        string lang,
        DateTimeOffset? createdAt = null,
        ICollection<EmbeddedImage>? images = null,
        ICollection<Facet>? facets = null,
        PostSelfLabels? labels = null,
        ICollection<string>? tags = null) : this(
            text: text,
            langs: [lang],
            createdAt: createdAt,
            images: images,
            facets: facets,
            labels: labels,
            tags: tags)
    {
    }

    /// <summary>
    /// Creates a new instance of a <see cref="PostBuilder"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="langs">The languages for the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="images">A collection of <see cref="EmbeddedImage"/>s to attach to the post, if any.</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s to attach to the post text, if any.</param>
    /// <param name="labels">Any self labels to apply to the post.</param>
    /// <param name="tags">Any tags to apply to the post.</param>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="text"/> for a <see cref="PostBuilder"/> is too long or <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> for the post is too long or <paramref name="images"/> contains too many images, or <paramref name="tags"/> has too many tags, or a tag that exceeds the maximum length.</exception>
    public PostBuilder(
        string? text,
        ICollection<string>? langs = null,
        DateTimeOffset? createdAt = null,
        ICollection<EmbeddedImage>? images = null,
        ICollection<Facet>? facets = null,
        PostSelfLabels? labels = null,
        ICollection<string>? tags = null)
    {
        DateTimeOffset postDate;

        if (createdAt is null)
        {
            postDate = DateTimeOffset.UtcNow;
        }
        else
        {
            postDate = createdAt.Value;
        }

        if (text is not null)
        {
            if (text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"Cannot be longer than {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (text.Length > Maximum.PostLengthInCharacters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"Cannot be longer than {Maximum.PostLengthInCharacters} characters.");
            }
        }

        if (images is not null && images.Count > Maximum.ImagesInPost)
        {
            throw new ArgumentOutOfRangeException(
                nameof(images),
                $"cannot have more than {Maximum.ImagesInPost} images.");
        }

        if (langs is not null)
        {
            ArgumentOutOfRangeException.ThrowIfZero(langs.Count);
        }

        if (tags is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

            foreach (string tag in tags)
            {
#pragma warning disable S3236 // Caller information arguments should not be provided explicitly
                ArgumentException.ThrowIfNullOrEmpty(tag, nameof(tags));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters, nameof(tags));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes, nameof(tags));
#pragma warning restore S3236 // Caller information arguments should not be provided explicitly
            }
        }

        if (!string.IsNullOrEmpty(text))
        {
            _post = new Post(text, createdAt: postDate);
        }
        else
        {
            // We need to create a Post instance with text, then blank it out again due to Post's validation/
            // The setter for Text is internal.
            _post = new Post("__NULL__", createdAt: postDate)
            {
                Text = text
            };
        }

        if (images is not null)
        {
            _embeddedImages.AddRange(images);
        }

        if (facets is not null)
        {
            _post.Facets = [.. facets];
        }

        if (labels is not null)
        {
            _post.SetSelfLabels(labels);
        }

        if (tags is not null)
        {
            _post.Tags = [.. tags];
        }

        if (langs is not null)
        {
            _post.Langs = langs;
        }
    }


    /// <summary>
    /// Gets a flag indicating whether this instance has any record text.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Length))]
    [MemberNotNullWhen(true, nameof(Text))]
    public bool HasText => _post.Text is not null && !string.IsNullOrEmpty(_post.Text);

    /// <summary>
    /// Gets the length of the post text, in characters.
    /// </summary>
    public int Length
    {
        get
        {
            lock (_syncLock)
            {
                return _post.Length;
            }
        }
    }

    /// <summary>
    /// Gets the length of the post text, in Utf8Bytes.
    /// </summary>
    public int Utf8Length
    {
        get
        {
            lock (_syncLock)
            {
                return _post.Utf8Length;
            }
        }
    }

    /// <summary>
    /// Gets the length of the post text, in graphemes.
    /// </summary>
    public int GraphemeLength
    {
        get
        {
            lock (_syncLock)
            {
                return _post.GraphemeLength;
            }
        }
    }

    /// <summary>
    /// Gets the maximum capacity of characters allowed in the record text of this instance
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Matching StringBuilder property.")]
    [SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Matching StringBuilder property.")]
    public int MaxCapacity => Maximum.PostLengthInCharacters;

    /// <summary>
    /// Gets the maximum capacity of graphemes allowed in the record text of this instance
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Matching StringBuilder property.")]
    [SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Matching StringBuilder property.")]
    public int MaxCapacityGraphemes => Maximum.PostLengthInGraphemes;

    /// <summary>
    /// Gets the maximum capacity of images allowed in this instance
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Matching StringBuilder property.")]
    [SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Matching StringBuilder property.")]
    public int MaxImages => Maximum.ImagesInPost;

    /// <summary>
    /// Gets a copy of the text for this instance.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when setting a value that is too long.</exception>
    public string? Text
    {
        get
        {
            lock (_syncLock)
            {
                return _post.Text;
            }
        }

        internal set
        {
            if (value is not null)
            {
                if (value.Length > Maximum.PostLengthInCharacters || value.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        $"text cannot have be longer than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
                }

                _post.Text = value;
            }
            else
            {
                _post.Langs = null;
            }
        }
    }

    /// <summary>
    /// Gets a copy of the list of languages for the post.
    /// </summary>
    public IEnumerable<string>? Langs
    {
        get
        {
            lock (_syncLock)
            {
                if (_post.Langs is null)
                {
                    return null;
                }
                else
                {
                    return new List<string>(_post.Langs).AsReadOnly();
                }
            }
        }

        internal set
        {
            lock (_syncLock)
            {
                if (value is not null)
                {
                    _post.Langs = [.. value];
                }
                else
                {
                    _post.Langs = null;
                }
            }
        }
    }

    /// <summary>
    /// Gets a list of facets for the post.
    /// </summary>
    public IReadOnlyCollection<Facet> Facets
    {
        get
        {
            lock (_syncLock)
            {
                if (_post.Facets is null)
                {
                    return [];
                }
                else
                {
                    return [.. _post.Facets];
                }
            }
        }

        internal set
        {
            lock (_syncLock)
            {
                _post.Facets = [.. value];
            }
        }
    }
        

    /// <summary>
    /// Gets a flag indicating whether this instance has any images
    /// </summary>
    [MemberNotNullWhen(true, nameof(Images))]
    public bool HasImages => _embeddedImages is not null && _embeddedImages.Count > 0;

    /// <summary>
    /// Gets a readonly list of the images for the post.
    /// </summary>
    public IReadOnlyCollection<EmbeddedImage> Images
    {
        get
        {
            lock (_syncLock)
            {
                return new List<EmbeddedImage>(_embeddedImages).AsReadOnly();
            }
        }

        private set
        {
            lock (_syncLock)
            {
                if (value is not null)
                {
                    _embeddedImages.AddRange(value);
                    _embeddedVideo = null;
                }
                else
                {
                    _embeddedImages.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Gets a flag indicating whether this instance has any tags
    /// </summary>
    [MemberNotNullWhen(true, nameof(Tags))]
    public bool HasTags => _post.Tags is not null && _post.Tags.Count > 0;

    /// <summary>
    /// Gets a copy of the tags for the post.
    /// </summary>
    public IEnumerable<string>? Tags
    {
        get
        {
            lock (_syncLock)
            {
                if (_post.Tags is null)
                {
                    return null;
                }
                else
                {
                    return new List<string>(_post.Tags).AsReadOnly();
                }
            }
        }

        private set
        {
            lock (_syncLock)
            {
                if (value is not null)
                {
                    _post.Tags = [.. value];
                }
                else
                {
                    _post.Tags = null;
                }
            }
        }
    }

    /// <summary>
    /// Gets a flag indicating whether this instance has an embedded video
    /// </summary>
    [MemberNotNullWhen(true, nameof(Video))]
    public bool HasVideo => _embeddedVideo is not null;

    /// <summary>
    /// Gets the embedded video for the post, if any.
    /// </summary>
    public EmbeddedVideo? Video
    {
        get
        {
            lock (_syncLock)
            {
                return _embeddedVideo;
            }
        }

        private set
        {
            lock (_syncLock)
            {
                if (value is not null)
                {
                    _embeddedImages.Clear();
                    _embeddedVideo = value;
                }
                else
                {
                    _embeddedVideo = null;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the <see cref="StrongReference"/> of the post being replied to.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when setting a value and <see cref="ThreadGateRules"/> is not <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   Replying to a post and quoting a post are mutually exclusive operations.
    ///   Setting <see cref="InReplyTo"/> will remove the <see cref="QuotePost"/> if one has been set.
    ///</para>
    /// </remarks>
    public ReplyReferences? InReplyTo
    {
        get
        {
            lock (_syncLock)
            {
                return _post.Reply;
            }
        }

        set
        {
            lock (_syncLock)
            {
                if (_threadGateRules is not null || DisableReplies)
                {
                    throw new ArgumentException("Cannot set InReplyTo if ThreadGateRules is not null.");
                }

                _post.Reply = value;

                if (value is not null && (_post.EmbeddedRecord is EmbeddedRecord || _post.EmbeddedRecord is EmbeddedRecordWithMedia))
                {
                    // Being a reply post excludes being a quote post.
                    QuotePost = null;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the <see cref="StrongReference"/> of the post being quoted.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when setting a value and the underlying post <see cref="EmbeddedRecord"/> is a type that cannot be quoted.</exception>
    /// <remarks>
    /// <para>
    ///   Replying to a post and quoting a post are mutually exclusive operations.
    ///   Setting <see cref="QuotePost"/> will remove the <see cref="InReplyTo"/> if one has been set.
    ///</para>
    /// </remarks>
    public StrongReference? QuotePost
    {
        get
        {
            lock (_syncLock)
            {
                if (_post.EmbeddedRecord is EmbeddedRecord embeddedRecord)
                {
                    return embeddedRecord.Record;
                }
                else
                {
                    return null;
                }
            }
        }

        set
        {
            lock (_syncLock)
            {
                if (value is null && _post.EmbeddedRecord is null)
                {
                    // Nothing to do
                }
                else if (value is null)
                {
                    if (_post.EmbeddedRecord is EmbeddedRecord || _post.EmbeddedRecord is EmbeddedRecordWithMedia)
                    {
                        // We already have a quote record, so let's just delete it.
                        _post.EmbeddedRecord = null;
                    }
                    else
                    {
                        throw new ArgumentException("postRecord has embed value other than an EmbeddedRecord or EmbeddedRecordWithMedia");
                    }
                }
                else
                {
                    _post.EmbeddedRecord = new EmbeddedRecord(value);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the thread gate rules to apply to the post.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <see cref="InReplyTo"/> is not <see langword="null"/> and rules are being set.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the number of rules being set is greater than the maximum allowed.</exception>
    public IReadOnlyCollection<ThreadGateRule>? ThreadGateRules
    {
        get
        {
            if (_threadGateRules is null)
            {
                return null;
            }
            else
            {
                return new List<ThreadGateRule>(_threadGateRules).AsReadOnly();
            }
        }

        set
        {
            lock (_syncLock)
            {
                if (InReplyTo is not null)
                {
                    throw new ArgumentException("Cannot set a thread gate on a post that is not a thread root.");
                }

                if (value is null)
                {
                    _threadGateRules = null;
                }
                else
                {
                    if (value.Count > Maximum.ThreadGateRules)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value), $"Cannot have more than {Maximum.ThreadGateRules} rules");
                    }

                    _threadGateRules = [.. value];
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a flag indicating whether replies to the post should be disabled.
    /// Can only be set for posts which are not in reply to another post (thread roots).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when attempt to set <see cref="DisableReplies"/> while <see cref="InReplyTo"/> has a value.</exception>
    public bool DisableReplies
    {
        get
        {
            return _disableReplies;
        }

        set
        {
            lock (_syncLock)
            {
                if (InReplyTo is not null)
                {
                    throw new ArgumentException("Cannot disable replies on a post that is in reply to another post.");
                }

                _disableReplies = value;

                if (_postGateRules != null)
                {
                    _postGateRules.Clear();
                }
                else
                {
                    _postGateRules = [];
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a flag indicating whether the new post should have embedding disabled.
    /// </summary>
    public bool DisableEmbedding
    {
        get;

        set
        {
            lock (_syncLock)
            {
                field = value;

                if (field)
                {

                    bool needToAddToRules = true;

                    if (_postGateRules != null)
                    {
                        foreach (var _ in from PostGateRule rule in _postGateRules
                                          where rule is DisableEmbeddingRule
                                          select new { })
                        {
                            needToAddToRules = false;
                        }
                    }

                    if (needToAddToRules)
                    {
                        _postGateRules ??= [];
                        _postGateRules.Add(new DisableEmbeddingRule());
                    }
                }
                else
                {
                    if (_postGateRules != null)
                    {
                        foreach (PostGateRule? rule in from PostGateRule rule in _postGateRules
                                             where rule is DisableEmbeddingRule
                                             select rule)
                        {
                            _postGateRules.Remove(rule);
                        }

                        if (_postGateRules.Count == 0)
                        {
                            _postGateRules = null;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets a flag indicating whether this instance has an embedded media record.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Embed))]
    public bool HasEmbed => Embed is not null;

    /// <summary>
    /// Gets or sets the embedded media for the post.
    /// </summary>
    /// <remarks>
    /// <para>Images and video only get processed to embedded records when ToPost() is called.</para>
    /// </remarks>
    public EmbeddedBase? Embed
    {
        get
        {
            return _post.EmbeddedRecord;
        }

        set
        {
            lock (_syncLock)
            {
                if (value is null)
                {
                    _post.EmbeddedRecord = null;
                }
                else
                {
                    if (Images is not null)
                    {
                        _embeddedImages.Clear();
                    }

                    _post.EmbeddedRecord = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets post gate rules to apply to the post.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the number of rules being set is greater than the maximum allowed.</exception>
    public IReadOnlyCollection<PostGateRule>? PostGateRules
    {
        get
        {
            if (_postGateRules is null)
            {
                return null;
            }
            else
            {
                return new List<PostGateRule>(_postGateRules).AsReadOnly();
            }
        }

        set
        {
            lock (_syncLock)
            {
                if (value is null)
                {
                    _postGateRules = null;
                    _disableReplies = false;
                }
                else
                {
                    if (value.Count > Maximum.PostGateRules)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value), $"Cannot have more than {Maximum.PostGateRules} rules.");
                    }

                    _postGateRules = [.. value];
                    foreach (var _ in from PostGateRule rule in _postGateRules
                                      where rule is DisableEmbeddingRule
                                      select new { })
                    {
                        _disableReplies = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Embeds the specified <see cref="EmbeddedBase" /> in the post.
    /// </summary>
    /// <param name="embeddedRecord">The record to embed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="embeddedRecord"/> is <see langword="null"/>.</exception>
    public void EmbedRecord(EmbeddedBase embeddedRecord)
    {
        ArgumentNullException.ThrowIfNull(embeddedRecord);
        _post.EmbeddedRecord = embeddedRecord;
    }

    /// <summary>
    /// Extracts facets from the post text, if any.
    /// </summary>
    /// <param name="facetExtractor">The facet extractor to use</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facetExtractor"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>If this instance of <see cref="PostBuilder"/> already has <see cref="Facet"/>s extraction will not run.</para>
    /// </remarks>
    public async Task ExtractFacets(IFacetExtractor facetExtractor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(facetExtractor);

        if (!string.IsNullOrEmpty(_post.Text) && Facets.Count == 0)
        {
            IList<Facet> facets = await facetExtractor.ExtractFacets(_post.Text, cancellationToken).ConfigureAwait(false);

            if (facets.Any())
            {
                _post.Facets = facets;
            }
        }
    }

    /// <summary>
    /// Sets the post <paramref name="text"/> for this instance
    /// </summary>
    /// <param name="text">The text to set for the post.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text exceeds the maximum allowed length.</exception>
    public PostBuilder WithText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        if (text.Length > MaxCapacity || text.GetGraphemeLength() > MaxCapacityGraphemes)
        {
            throw new ArgumentOutOfRangeException(nameof(text),string.Format(null, s_postTextExceedsMaxLength, MaxCapacity, MaxCapacityGraphemes));
        }

        _post.Text = text;
        return this;
    }

    /// <summary>
    /// Sets the post to be a reply to the post specified by the <paramref name="replyReferences"/>.
    /// </summary>
    /// <param name="replyReferences">The <see cref="ReplyReferences"/> to set as the reply target.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="replyReferences"/> is <see langword="null"/>.</exception>
    public PostBuilder ReplyTo(ReplyReferences replyReferences)
    {
        ArgumentNullException.ThrowIfNull(replyReferences);
        InReplyTo = replyReferences;
        return this;
    }

    /// <summary>
    /// Sets the post to be a reply to the post specified by the <paramref name="postReference"/>.
    /// </summary>
    /// <param name="postReference">The <see cref="StrongReference"/> to set as the reply target.</param>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for retrieving reply references.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postReference"/> or <paramref name="agent"/> is <see langword="null"/>.</exception>
    /// <exception cref="BlueskyException">Thrown when the reply references for <paramref name="postReference"/> could not be retrieved.</exception>
    public async Task<PostBuilder> ReplyTo(StrongReference postReference, BlueskyAgent agent)
    {
        ArgumentNullException.ThrowIfNull(postReference);
        ArgumentNullException.ThrowIfNull(agent);

        AtProtoHttpResult<ReplyReferences> replyReferences = await agent.GetReplyReferences(postReference, CancellationToken.None).ConfigureAwait(false);

        if (replyReferences.Succeeded)
        {
            InReplyTo = replyReferences.Result;
        }
        else
        {
            throw new BlueskyException("Failed to get reply references for the provided post reference.");
        }

        return this;
    }

    /// <summary>
    /// Applies the user's interaction preferences to this instance.
    /// </summary>
    /// <param name="interactionPreferences">The user's interaction preferences.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="interactionPreferences"/> is <see langword="null"/>.</exception>
    public void ApplyInteractionPreferences(InteractionPreferences interactionPreferences)
    {
        ArgumentNullException.ThrowIfNull(interactionPreferences);
        lock (_syncLock)
        {
            if (interactionPreferences.ThreadGateAllowRules is not null && _threadGateRules is null)
            {
                _threadGateRules = [.. interactionPreferences.ThreadGateAllowRules];
            }

            if (interactionPreferences.PostGateEmbeddingRules is not null && _postGateRules is null)
            {
                _postGateRules = [.. interactionPreferences.PostGateEmbeddingRules];
            }
        }
    }

    /// <summary>
    /// Quotes a record specified by <paramref name="strongReference"/> and adds it to this instance.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference"/> to quote.</param>
    /// <returns>This instance of <see cref="PostBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="strongReference"/> does not point to a valid Bluesky Post record.</exception>
    public PostBuilder Quote(StrongReference strongReference)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        if (strongReference.Uri.Collection != CollectionNsid.Post)
        {
            throw new ArgumentException(Properties.Resources.PostBuilderAddQuoteToNonPostError, nameof(strongReference));
        }

        Add(strongReference);
        return this;
    }

    /// <summary>
    /// Quotes a record specified by <paramref name="strongReference"/> and adds it to the specified <paramref name="postBuilder" />.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the quote to.</param>
    /// <param name="strongReference">The <see cref="StrongReference"/> to quote.</param>
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="strongReference"/> does not point to a valid Bluesky Post record.</exception>
    public static PostBuilder Quote(PostBuilder postBuilder, StrongReference strongReference)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(strongReference);

        if (strongReference.Uri.Collection != CollectionNsid.Post)
        {
            throw new ArgumentException(Properties.Resources.PostBuilderAddQuoteToNonPostError, nameof(strongReference));
        }

        postBuilder.Quote(strongReference);
        return postBuilder;
    }

    /// <summary>
    /// Sets the self labels for the post to the values specified in <paramref name="labels"/>.
    /// </summary>
    /// <param name="labels">The self label values to set.</param>
    /// <returns>This instance of <see cref="PostBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is <see langword="null"/>.</exception>
    public PostBuilder SetSelfLabels(PostSelfLabels labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        _post.SetSelfLabels(labels);
        return this;
    }

    /// <summary>
    /// Sets the self labels for the post to the values specified in <paramref name="labels"/>.
    /// </summary>
    /// <param name="labels">The self label values to set.</param>
    /// <returns>This instance of <see cref="PostBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is <see langword="null"/>.</exception>
    public PostBuilder Label(PostSelfLabels labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return SetSelfLabels(labels);
    }

    /// <summary>
    /// Marks a media containing post, as containing graphic content.
    /// </summary>
    /// <returns>This instance of <see cref="PostBuilder"/>.</returns>
    public PostBuilder ContainsGraphicMedia()
    {
        return Add(new SelfLabel(SelfLabelValues.GraphicMedia));
    }

    /// <summary>
    /// Marks a media containing post as containing nudity.
    /// </summary>
    /// <returns>This instance of <see cref="PostBuilder"/>.</returns>
    public PostBuilder ContainsNudity()
    {
        return Add(new SelfLabel(SelfLabelValues.Nudity));
    }

    /// <summary>
    /// Marks a media containing post as containing porn.
    /// </summary>
    /// <returns>This instance of <see cref="PostBuilder"/>.</returns>
    public PostBuilder ContainsPorn()
    {
        return Add(new SelfLabel(SelfLabelValues.Porn));
    }

    /// <summary>
    /// Marks a media containing post as containing sexual content.
    /// </summary>
    /// <returns>This instance of <see cref="PostBuilder"/>.</returns>
    public PostBuilder ContainsSexualContent()
    {
        return Add(new SelfLabel(SelfLabelValues.Sexual));
    }

    /// <summary>
    /// Converts the value of this instance to a <see cref="Post"/>.
    /// </summary>
    /// <returns>A <see cref="Post"/> whose value is the same as this instance.</returns>
    /// <exception cref="PostBuilderException">
    ///  Thrown when <see cref="Text"/> is <see langword="null"/> or empty and this instance has no images, embedded records or video, or
    ///  this instance is both a reply and a quote, or this instance has both images and video.
    /// </exception>
    public Post ToPost()
    {
        lock (_syncLock)
        {
            if (!HasText && !HasImages && !HasEmbed && !HasVideo)
            {
                throw new PostBuilderException(Properties.Resources.EmptyPostTextValidationError);
            }

            if (HasVideo && HasImages)
            {
                throw new PostBuilderException(Properties.Resources.PostCannotHaveImagesAndVideoValidationError);
            }

            if (HasImages)
            {
                if (InReplyTo is null && QuotePost is null)
                {
                    // Plain old post
                    _post.EmbeddedRecord = new EmbeddedImages(_embeddedImages);
                }
                else if (QuotePost is not null)
                {
                    // Quote post, so we need fix up the embedded record to include images.
                    _post.EmbeddedRecord = new EmbeddedRecordWithMedia(new EmbeddedRecord(QuotePost), new EmbeddedImages(_embeddedImages));
                }
                else
                {
                    // Reply post
                    _post.EmbeddedRecord = new EmbeddedImages(_embeddedImages);
                }
            }

            if (HasVideo)
            {
                if (InReplyTo is null && QuotePost is null)
                {
                    // Plain old post
                    _post.EmbeddedRecord = _embeddedVideo;
                }
                else if (QuotePost is not null)
                {
                    // Quote post, so we need fix up the embedded record to include the video.
                    _post.EmbeddedRecord = new EmbeddedRecordWithMedia(new EmbeddedRecord(QuotePost), Video);
                }
                else
                {
                    // Reply post
                    _post.EmbeddedRecord = _embeddedVideo;
                }
            }

            if (!HasImages && !HasVideo && _post.Labels is not null)
            {
                throw new PostBuilderException(Properties.Resources.PostHasLabelsButNoMediaValidationError);
            }

            return new Post(_post);
        }
    }

    /// <summary>
    /// Converts the specified <paramref name="postBuilder"/> to a <see cref="Post"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to convert.</param>
    /// <returns>A <see cref="Post"/> whose built from <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is <see langword="null"/>.</exception>
    public static implicit operator Post(PostBuilder postBuilder)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        return postBuilder.ToPost();
    }

    /// <summary>
    /// Creates a new <see cref="PostBuilder"/>.
    /// </summary>
    /// <returns>A new instance of <see cref="PostBuilder"/>.</returns>
    public static PostBuilder Create()
    {
        return new PostBuilder();
    }

    /// <summary>
    /// Creates a new <see cref="PostBuilder"/>.
    /// </summary>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the creation date and time of the post.</param>
    /// <returns>A new instance of <see cref="PostBuilder"/>.</returns>
    public static PostBuilder Create(DateTimeOffset createdAt)
    {
        return new PostBuilder(text: null, createdAt: createdAt);
    }

    /// <summary>
    /// Creates a new <see cref="PostBuilder"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="lang">The language of the post, as an ISO language code.</param>
    /// <returns>A new instance of <see cref="PostBuilder"/>.</returns>
    public static PostBuilder Create(string text, string lang)
    {
        return new PostBuilder(text, lang: lang);
    }

    /// <summary>
    /// Creates a new <see cref="PostBuilder"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="langs">The languages the post contains, as ISO language codes.</param>
    /// <returns>A new instance of <see cref="PostBuilder"/>.</returns>
    public static PostBuilder Create(string text, string[] langs)
    {
        return new PostBuilder(text, langs: langs);
    }

    /// <summary>
    /// Determines whether two <see cref="PostBuilder"/> instances are equal.
    /// </summary>
    /// <param name="other">The <see cref="PostBuilder"/> to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the specified <see cref="PostBuilder"/> is equal to the current instance; otherwise, <see langword="false"/>.</returns>
    public bool Equals(PostBuilder? other) => other is not null && 
        EqualityComparer<Post>.Default.Equals(_post, other._post) &&
        EqualityComparer<List<EmbeddedImage>>.Default.Equals(_embeddedImages, other._embeddedImages) &&
        EqualityComparer<List<ThreadGateRule>?>.Default.Equals(_threadGateRules, other._threadGateRules) &&
        EqualityComparer<List<PostGateRule>?>.Default.Equals(_postGateRules, other._postGateRules) &&
        DisableEmbedding == other.DisableEmbedding;

    /// <summary>
    /// Determines whether two object instances are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns><see langword="true"/> if the specified object is equal to the current object; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => obj is PostBuilder other && Equals(other);

    /// <summary>
    /// Determines whether two specified <see cref="PostBuilder"/>s the same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="PostBuilder"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="PostBuilder"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(PostBuilder? lhs, PostBuilder? rhs)
    {
        if (lhs is null)
        {
            if (rhs is null)
            {
                return true;
            }

            return false;
        }

        return lhs == rhs;
    }

    /// <summary>
    /// Determines whether two specified <see cref="PostBuilder"/>s dot not have same value."/>
    /// </summary>
    /// <param name="lhs">The first <see cref="PostBuilder"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="PostBuilder"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(PostBuilder? lhs, PostBuilder? rhs) => !(lhs == rhs);

    /// <summary>
    /// Gets a hash code for the current object.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    [SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "Locking and copying to avoid field changes whilst hash code is computed.")]
    public override int GetHashCode()
    {
        List<EmbeddedImage>? embeddedImages = null;
        List<ThreadGateRule>? threadGateRules = null;
        List<PostGateRule>? postGateRules = null;

        lock (_syncLock)
        {
            Post postRecord = new (_post);

            if (_embeddedImages is not null)
            {
                embeddedImages = [.. _embeddedImages];
            }

            if (_threadGateRules is not null)
            {
                threadGateRules = [.. _threadGateRules];
            }

            if (_postGateRules is not null)
            {
                postGateRules = [.. _postGateRules];
            }

            if (embeddedImages is null && threadGateRules is null && postGateRules is null)
            {
                return postRecord.GetHashCode();
            }
            else
            {
                return HashCode.Combine(postRecord, embeddedImages, threadGateRules, postGateRules, _embeddedVideo);
            }
        }
    }

    private static ByteSlice GetFacetPosition(string? currentText, string textToAdd)
    {
        int startingPosition = 0;
        if (currentText is not null)
        {
            startingPosition = currentText.GetUtf8Length();
        }

        ByteSlice byteSlice = new(startingPosition, startingPosition + textToAdd.GetUtf8Length());

        return byteSlice;
    }
}
