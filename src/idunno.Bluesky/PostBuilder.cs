// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky
{
    /// <summary>
    /// A builder to allow building of a Post record in a more friendly manner.
    /// </summary>
    public sealed class PostBuilder
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
        /// <exception cref="ArgumentException">Thrown when the <paramref name="text"/> for a <see cref="PostBuilder"/> is too long or <paramref name="tags"/> contains a null or empty tag.</exception>
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
        /// <remarks>
        /// <para>
        ///   Replying to a post and quoting a post are mutually exclusive operations.
        ///   Setting <see cref="InReplyTo"/> will remove the <see cref="QuotePost"/> if one has been set.
        ///</para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when setting a value and <see cref="ThreadGateRules"/> is not null.</exception>
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
        /// <exception cref="ArgumentException">Thrown when <see cref="InReplyTo"/> is not null and rules are being set.</exception>
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="embeddedRecord"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="facetExtractor"/> is null.</exception>
        /// <remarks>
        /// <para>If this instance of <see cref="PostBuilder"/> already has <see cref="Facet"/>s extraction will not run.</para>
        /// </remarks>
        public async Task ExtractFacets(IFacetExtractor facetExtractor, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(facetExtractor);

            if (!string.IsNullOrEmpty(_post.Text) && Facets.Count == 0)
            {
                _post.Facets = await facetExtractor.ExtractFacets(_post.Text, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Appends a copy of the specified string to the record text of this instance.
        /// </summary>
        /// <param name="value">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when enlarging the the record text of this instance would exceed <see cref="MaxCapacity"/> or <see cref="MaxCapacityGraphemes"/>.</exception>
        public PostBuilder Append(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return this;
            }

            lock (_syncLock)
            {
                if (value.Length > MaxCapacity || value.GetGraphemeLength() > MaxCapacityGraphemes)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"string cannot have a length greater than {MaxCapacity} characters, or {MaxCapacityGraphemes} graphemes.");
                }

                if (_post.Text is null)
                {
                    _post.Text = value;
                    return this;
                }

                int newLength = value.Length + _post.Text.Length;
                int newGraphemeLength = value.GetGraphemeLength() + _post.Text.GetGraphemeLength();

                if (newLength > MaxCapacity || newGraphemeLength > MaxCapacityGraphemes)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"Appending would cause the post record to have a text property of length greater than {MaxCapacity} characters, or {MaxCapacityGraphemes} graphemes.");
                }
                else
                {
                    _post.Text += value;
                    return this;
                }
            }
        }

        /// <summary>
        /// Appends a <see cref="Mention"/> to the text and and facet features of this instance.
        /// </summary>
        /// <param name="mention">The <see cref="Mention"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mention"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="mention"/>'s text is null or white space.</exception>
        public PostBuilder Append(Mention mention)
        {
            ArgumentNullException.ThrowIfNull(mention);
            ArgumentException.ThrowIfNullOrWhiteSpace(mention.Text);

            lock (_syncLock)
            {
                ByteSlice byteSlice = GetFacetPosition(_post.Text, mention.Text);
                _post.Text += mention.Text;

                MentionFacetFeature mentionFacetFeature = new(mention.Did);
                List<FacetFeature> features =
                    [
                        mentionFacetFeature
                    ];

                _post.Facets ??= [];
                _post.Facets.Add(new Facet(byteSlice, features));

                return this;
            }
        }

        /// <summary>
        /// Appends a copy of the specified character to the record text of this instance.
        /// </summary>
        /// <param name="value">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when enlarging the the record text of this instance would exceed <see cref="MaxCapacity"/> or <see cref="MaxCapacityGraphemes"/>.</exception>
        public PostBuilder Append(char value)
        {
            return Append(value.ToString());
        }

        /// <summary>
        /// Appends a specified number of copies of the string representation of a Unicode character to this instance.
        /// </summary>
        /// <param name="value">The character to append.</param>
        /// <param name="repeatCount">The number of times to append <paramref name="value"/>.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="repeatCount"/> is &lt;=0.</exception>
        public PostBuilder Append(char value, int repeatCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(repeatCount);
            ArgumentOutOfRangeException.ThrowIfZero(repeatCount);

            return Append(new string(value, repeatCount));
        }

        /// <summary>
        /// Appends a <see cref="HashTag"/> to the text and facet features of this instance.
        /// </summary>
        /// <param name="hashTag">The <see cref="HashTag"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashTag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="hashTag"/>'s text is null or whitespace.</exception>
        public PostBuilder Append(HashTag hashTag)
        {
            ArgumentNullException.ThrowIfNull(hashTag);
            ArgumentException.ThrowIfNullOrWhiteSpace(hashTag.Text);

            lock (_syncLock)
            {
                ByteSlice byteSlice = GetFacetPosition(_post.Text, hashTag.Text);
                _post.Text += hashTag.Text;

                TagFacetFeature tagFacetFeature = new(hashTag.Tag);
                List<FacetFeature> features =
                    [
                        tagFacetFeature
                    ];

                _post.Facets ??= [];
                _post.Facets.Add(new Facet(byteSlice, features));

                return this;
            }
        }

        /// <summary>
        /// Appends a <see cref="Link"/> to the text and facet features of this instance.
        /// </summary>
        /// <param name="link">The <see cref="Link"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when  <paramref name="link"/> is null or its text is null or whitespace.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="link"/>'s is null or whitespace.</exception>
        public PostBuilder Append(Link link)
        {
            ArgumentNullException.ThrowIfNull(link);
            ArgumentException.ThrowIfNullOrWhiteSpace(link.Text);

            lock (_syncLock)
            {
                ByteSlice byteSlice = GetFacetPosition(_post.Text, link.Text);
                _post.Text += link.Text;

                LinkFacetFeature linkFacetFeature = new(link.Uri);
                List<FacetFeature> features =
                    [
                        linkFacetFeature
                    ];

                _post.Facets ??= [];
                _post.Facets.Add(new Facet(byteSlice, features));

                return this;
            }
        }

        /// <summary>
        /// Applies the user's interaction preferences to this instance.
        /// </summary>
        /// <param name="interactionPreferences">The user's interaction preferences.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="interactionPreferences"/> is null.</exception>
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
        /// Adds a copy of the specified string to the record text to the specified <paramref name="postBuilder"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the string to.</param>
        /// <param name="s">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is null.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, string s)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            return postBuilder + s;
        }

        /// <summary>
        /// Adds a copy of the specified character to the record text to the specified <paramref name="postBuilder"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the string to.</param>
        /// <param name="c">The character to append</param>
        /// <param name="repeatCount">The number of times to repeat the <paramref name="c" />.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is null.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, char c, int repeatCount = 1)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            return postBuilder.Append(c, repeatCount);
        }

        /// <summary>
        /// Adds a <see cref="Link"/> to the text and facet features of the specified <paramref name="postBuilder"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the link to.</param>
        /// <param name="link">The <see cref="Link"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="link"/> is null.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, Link link)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(link);

            return postBuilder.Append(link);
        }

        /// <summary>
        /// Adds a <see cref="Mention"/> to the text and and facet features to the specified <paramref name="postBuilder"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the mention to.</param>
        /// <param name="mention">The <see cref="Mention"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="mention"/> is null.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, Mention mention)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(mention);

            return postBuilder.Append(mention);
        }

        /// <summary>
        /// Adds a <see cref="HashTag"/> to the text and facet features to the specified <paramref name="postBuilder"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the hash tag to.</param>
        /// <param name="hashTag">The <see cref="HashTag"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="hashTag"/> is null.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, HashTag hashTag)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(hashTag);

            return postBuilder.Append(hashTag);
        }

        /// <summary>
        /// Adds a <see cref="EmbeddedImage"/> to this instance.
        /// </summary>
        /// <param name="image">The <see cref="EmbeddedImage"/> to embed.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number of images in this instance is already equal to <see cref="MaxImages"/>.</exception>
        public PostBuilder Add(EmbeddedImage image)
        {
            ArgumentNullException.ThrowIfNull(image);

            lock (_syncLock)
            {
                if (_embeddedImages.Count == MaxImages)
                {
                    throw new ArgumentOutOfRangeException(nameof(image), $"cannot have more than {MaxImages} in a post.");
                }

                _embeddedImages.Add(image);
                _embeddedVideo = null;
                return this;
            }
        }

        /// <summary>
        /// Adds a <see cref="EmbeddedImage"/> to the specified <paramref name="postBuilder"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the image to.</param>
        /// <param name="image">The <see cref="EmbeddedImage"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number of images in this instance is already equal to <see cref="MaxImages"/>.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, EmbeddedImage image)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(image);

            lock (postBuilder._syncLock)
            {
                if (postBuilder._embeddedImages.Count == postBuilder.MaxImages)
                {
                    throw new ArgumentOutOfRangeException(nameof(image), $"cannot have more than {postBuilder.MaxImages} in a post.");
                }

                postBuilder._embeddedImages.Add(image);
                postBuilder._embeddedVideo = null;

                return postBuilder;
            }
        }

        /// <summary>
        /// Adds a collection <see cref="EmbeddedImage"/>s to this instance.
        /// </summary>
        /// <param name="images">The collection of <see cref="EmbeddedImage"/> to embed.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="images"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when adding the images in this instance is will result in an image count &gt;<see cref="MaxImages"/>.</exception>
        public PostBuilder Add(ICollection<EmbeddedImage> images)
        {
            ArgumentNullException.ThrowIfNull(images);

            lock (_syncLock)
            {
                if ((_embeddedImages.Count + images.Count) > MaxImages)
                {
                    throw new ArgumentOutOfRangeException(nameof(images), $"cannot have more than {MaxImages} in a post.");
                }

                _embeddedImages.AddRange(images);
                _embeddedVideo = null;
                return this;
            }
        }

        /// <summary>
        /// Adds a collection of <see cref="EmbeddedImage"/>s to the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the images to.</param>
        /// <param name="images">The collection of <see cref="EmbeddedImages"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="images"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when adding the images in this instance is will result in an image count &gt;<see cref="MaxImages"/>.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, ICollection<EmbeddedImage> images)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(images);

            lock (postBuilder._syncLock)
            {
                if ((postBuilder._embeddedImages.Count + images.Count) > postBuilder.MaxImages)
                {
                    throw new ArgumentOutOfRangeException(nameof(images), $"cannot have more than {postBuilder.MaxImages} in a post.");
                }

                postBuilder._embeddedImages.AddRange(images);
                postBuilder._embeddedVideo = null;
                return postBuilder;
            }
        }

        /// <summary>
        /// Adds a <see cref="EmbeddedVideo"/> to this instance.
        /// </summary>
        /// <param name="video">The <see cref="EmbeddedVideo"/> to embed.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null.</exception>
        public PostBuilder Add(EmbeddedVideo video)
        {
            ArgumentNullException.ThrowIfNull(video);

            lock (_syncLock)
            {
                Video = video;
                _embeddedImages.Clear();
                return this;
            }
        }

        /// <summary>
        /// Adds a <see cref="EmbeddedVideo"/> to the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the video to.</param>
        /// <param name="video">The <see cref="EmbeddedVideo"/> to embed.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, EmbeddedVideo video)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(video);

            lock (postBuilder._syncLock)
            {
                postBuilder.Video = video;
                postBuilder._embeddedImages.Clear();
                return postBuilder;
            }
        }

        /// <summary>
        /// Adds a copy of the specified string to the record text of the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the images to.</param>
        /// <param name="s">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, string s)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            return postBuilder.Append(s);
        }

        /// <summary>
        /// Adds the specified character to the record text of the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the images to.</param>
        /// <param name="c">The character to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, char c)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            return postBuilder.Append(c);
        }

        /// <summary>
        /// Adds a <see cref="Mention"/> to the text and facet features to the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the mention to.</param>
        /// <param name="mention">The <see cref="Mention"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="mention"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, Mention mention)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(mention);

            return postBuilder.Append(mention);
        }

        /// <summary>
        /// Adds a <see cref="HashTag"/> to the text and facet features to the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the hash tag to.</param>
        /// <param name="hashTag">The <see cref="HashTag"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="hashTag"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, HashTag hashTag)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(hashTag);

            return postBuilder.Append(hashTag);
        }

        /// <summary>
        /// Adds a <see cref="Link"/> to the text and facet features to the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the link tag to.</param>
        /// <param name="link">The <see cref="Link"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="link"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, Link link)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(link);

            return postBuilder.Append(link);
        }

        /// <summary>
        /// Adds an <see cref="EmbeddedImage"/> to the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the image to.</param>
        /// <param name="image">The <see cref="EmbeddedImage"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="image"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, EmbeddedImage image)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(image);

            return Add(postBuilder, image);
        }

        /// <summary>
        /// Adds a collection of <see cref="EmbeddedImage"/>s to the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the images to.</param>
        /// <param name="images">The collection of <see cref="EmbeddedImage"/>s to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="images"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, ICollection<EmbeddedImage> images)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(images);

            return Add(postBuilder, images);
        }

        /// <summary>
        /// Adds an <see cref="EmbeddedVideo"/> to the specified <paramref name="postBuilder" />.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the image to.</param>
        /// <param name="video">The <see cref="EmbeddedVideo"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="video"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, EmbeddedVideo video)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(video);

            return Add(postBuilder, video);
        }

        /// <summary>
        /// Sets the self labels for the post to the values specified in <paramref name="labels"/>.
        /// </summary>
        /// <param name="labels">The self label values to set.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is null.</exception>
        public void SetSelfLabels(PostSelfLabels labels)
        {
            ArgumentNullException.ThrowIfNull(labels);

            _post.SetSelfLabels(labels);
        }

        /// <summary>
        /// Converts the value of this instance to a <see cref="Post"/>.
        /// </summary>
        /// <returns>A <see cref="Post"/> whose value is the same as this instance.</returns>
        /// <exception cref="PostBuilderException">
        ///  Thrown when <see cref="Text"/> is null or empty and this instance has no images, embedded records or video, or
        ///  this instance is both a reply and a quote, or this instance has both images and video.
        /// </exception>
        public Post ToPost()
        {
            lock (_syncLock)
            {
                if (!HasText && !HasImages && !HasEmbed && !HasVideo)
                {
                    throw new PostBuilderException("Post text cannot be null or empty unless there are images, video or another type of embedded record.");
                }

                if (InReplyTo is not null && QuotePost is not null)
                {
                    throw new PostBuilderException("Post cannot be both a reply and a quote");
                }

                if (HasVideo && HasImages)
                {
                    throw new PostBuilderException("Post cannot have both images and video");
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

                return new Post(_post);
            }
        }

        /// <summary>
        /// Converts the specified <paramref name="postBuilder"/> to a <see cref="Post"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to convert.</param>
        /// <returns>A <see cref="Post"/> whose built from <paramref name="postBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is null.</exception>
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
        /// Determines whether two object instances are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><see langword="true"/> if the specified object is equal to the current object; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? obj) => obj is PostBuilder builder && EqualityComparer<Post>.Default.Equals(_post, builder._post) && EqualityComparer<List<EmbeddedImage>>.Default.Equals(_embeddedImages, builder._embeddedImages) && EqualityComparer<List<ThreadGateRule>?>.Default.Equals(_threadGateRules, builder._threadGateRules) && EqualityComparer<List<PostGateRule>?>.Default.Equals(_postGateRules, builder._postGateRules) && DisableEmbedding == builder.DisableEmbedding;

        /// <summary>
        /// Determines whether two specified <see cref="PostBuilder"/>s the same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="PostBuilder"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="PostBuilder"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, false.</returns>
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

            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Determines whether two specified <see cref="PostBuilder"/>s dot not have same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="PostBuilder"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="PostBuilder"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, false.</returns>
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
}
