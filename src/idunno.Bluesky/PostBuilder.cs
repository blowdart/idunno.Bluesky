// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.RichText;
using static System.Net.Mime.MediaTypeNames;

namespace idunno.Bluesky
{
    /// <summary>
    /// A class to allow building of a Post record in a more friendly manner.
    /// </summary>
    public sealed class PostBuilder
    {
        private readonly object _syncLock = new ();

        private readonly Post _postRecord;
        private readonly List<EmbeddedImage> _embeddedImages = new();
        private EmbeddedVideo? _embeddedVideo;
        private List<ThreadGateRule>? _threadGateRules;
        private List<PostGateRule>? _postGateRules;
        private bool _disableReplies;
        private bool _disableEmbedding;

        /// <summary>
        /// Creates a new instance of a <see cref="PostBuilder"/>.
        /// </summary>
        public PostBuilder()
        {
            _postRecord = new Post() { CreatedAt = DateTimeOffset.UtcNow };
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PostBuilder"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="images">A collection of <see cref="EmbeddedImage"/>s to attach to the post, if any.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s to attach to the post text, if any.</param>
        /// <param name="labels">Any self labels to apply to the post.</param>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="text"/> for a <see cref="PostBuilder"/> is too long.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="text"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If the text for the post is too long or too many images are specified.</exception>
        public PostBuilder(string text, ICollection<EmbeddedImage>? images = null, IList<Facet>? facets = null, PostSelfLabels? labels = null) : this()
        {
            ArgumentNullException.ThrowIfNull(text);

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


            if (images is not null && images.Count > Maximum.ImagesInPost)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(images),
                    $"cannot have more than {Maximum.ImagesInPost} images.");
            }

            _postRecord.Text = text;

            if (images is not null)
            {
                _embeddedImages.AddRange(images);
            }

            if (facets is not null)
            {
                _postRecord.Facets = new List<Facet>(facets);
            }

            if (labels is not null)
            {
                _postRecord.SetSelfLabels(labels);
            }
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PostBuilder"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="language">The language for the post.</param>
        /// <param name="labels">Any self labels to apply to the post.</param>
        public PostBuilder(string text, string language, PostSelfLabels? labels = null) : this(text, languages: new string[] { language }, images: null, facets : null, labels: labels)
        {
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PostBuilder"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="languages">The languages for the post.</param>
        /// <param name="images">An optional collection of <see cref="EmbeddedImage"/>s to attach to the post.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s to attach to the post text, if any.</param>
        /// <param name="labels">Any self labels to apply to the post.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="languages"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="languages"/> contains no entries.</exception>
        public PostBuilder(string text, string[] languages, ICollection<EmbeddedImage>? images = null, IList<Facet>? facets = null, PostSelfLabels? labels = null) : this(text, images, facets, labels)
        {
            ArgumentNullException.ThrowIfNull(languages);
            ArgumentOutOfRangeException.ThrowIfZero(languages.Length);

            _postRecord.Langs = languages;
        }

        /// <summary>
        /// Gets a flag indicating whether this instance has any record text.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Length))]
        public bool HasText => _postRecord.Text is not null && !string.IsNullOrEmpty(_postRecord.Text);

        /// <summary>
        /// Gets the length of the post text, in characters.
        /// </summary>
        public int Length
        {
            get
            {
                lock (_syncLock)
                {
                    return _postRecord.Length;
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
                    return _postRecord.Utf8Length;
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
                    return _postRecord.GraphemeLength;
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
        public string? Text
        {
            get
            {
                lock (_syncLock)
                {
                    return _postRecord.Text;
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

                    _postRecord.Text = value;
                }
                else
                {
                    _postRecord.Langs = null;
                }
            }
        }

        /// <summary>
        /// Gets a copy of the list of languages for the post.
        /// </summary>
        public IReadOnlyList<string>? Languages
        {
            get
            {
                lock (_syncLock)
                {
                    if (_postRecord.Langs is null)
                    {
                        return null;
                    }
                    else
                    {
                        return new List<string>(_postRecord.Langs).AsReadOnly();
                    }
                }
            }

            internal set
            {
                lock (_syncLock)
                {
                    if (value is not null)
                    {
                        _postRecord.Langs = new List<string>(value);
                    }
                    else
                    {
                        _postRecord.Langs = null;
                    }
                }
            }
        }

        /// <summary>
        /// Gets a list of facets for the post.
        /// </summary>
        public IList<Facet> Facets
        {
            get
            {
                lock (_syncLock)
                {
                    if (_postRecord.Facets is null)
                    {
                        return new List<Facet>();
                    }
                    else
                    {
                        return new List<Facet>(_postRecord.Facets);
                    }
                }
            }

            internal set
            {
                lock (_syncLock)
                {
                    _postRecord.Facets = new List<Facet>(value);
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
        public IReadOnlyList<EmbeddedImage> Images
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
                    return _postRecord.Reply;
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

                    _postRecord.Reply = value;

                    if (value is not null && (_postRecord.EmbeddedRecord is EmbeddedRecord || _postRecord.EmbeddedRecord is EmbeddedRecordWithMedia))
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
                    if (_postRecord.EmbeddedRecord is EmbeddedRecord embeddedRecord)
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
                    if (value is null && _postRecord.EmbeddedRecord is null)
                    {
                        // Nothing to do
                    }
                    else if (value is null)
                    {
                        if (_postRecord.EmbeddedRecord is EmbeddedRecord || _postRecord.EmbeddedRecord is EmbeddedRecordWithMedia)
                        {
                            // We already have a quote record, so let's just delete it.
                            _postRecord.EmbeddedRecord = null;
                        }
                        else
                        {
                            throw new ArgumentException("postRecord has embed value other than an EmbeddedRecord or EmbeddedRecordWithMedia");
                        }
                    }
                    else
                    {
                        _postRecord.EmbeddedRecord = new EmbeddedRecord(value);
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

                        _threadGateRules = new List<ThreadGateRule>(value);
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
                        _postGateRules = new List<PostGateRule>();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating whether the new post should have embedding disabled.
        /// </summary>
        public bool DisableEmbedding
        {
            get
            {
                return _disableEmbedding;
            }

            set
            {
                lock (_syncLock)
                {
                    _disableEmbedding = value;

                    if (_disableEmbedding)
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
                            _postGateRules ??= new List<PostGateRule>();
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
                return _postRecord.EmbeddedRecord;
            }

            set
            {
                lock (_syncLock)
                {
                    if (value is null)
                    {
                        _postRecord.EmbeddedRecord = null;
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

                        _postGateRules = new List<PostGateRule>(value);
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
            _postRecord.EmbeddedRecord = embeddedRecord;
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

                if (_postRecord.Text is null)
                {
                    _postRecord.Text = value;
                    return this;
                }

                int newLength = value.Length + _postRecord.Text.Length;
                int newGraphemeLength = value.GetGraphemeLength() + _postRecord.Text.GetGraphemeLength();

                if (newLength > MaxCapacity || newGraphemeLength > MaxCapacityGraphemes)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"Appending would cause the post record to have a text property of length greater than {MaxCapacity} characters, or {MaxCapacityGraphemes} graphemes.");
                }
                else
                {
                    _postRecord.Text += value;
                    return this;
                }
            }
        }

        /// <summary>
        /// Appends a <see cref="Mention"/> to the text and and facet features of this instance.
        /// </summary>
        /// <param name="mention">The <see cref="Mention"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="mention"/> is null or its text is null or white space.</exception>
        public PostBuilder Append(Mention mention)
        {
            ArgumentNullException.ThrowIfNull(mention);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(mention.Text);

            lock (_syncLock)
            {
                ByteSlice byteSlice = GetFacetPosition(_postRecord.Text, mention.Text);
                _postRecord.Text += mention.Text;

                MentionFacetFeature mentionFacetFeature = new(mention.Did);
                List<FacetFeature> features = new()
                    {
                        mentionFacetFeature
                    };

                _postRecord.Facets ??= new List<Facet>();
                _postRecord.Facets.Add(new Facet(byteSlice, features));

                return this;
            }
        }

        /// <summary>
        /// Appends a copy of the specified character to the record text of this instance.
        /// </summary>
        /// <param name="value">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the the record text of this instance would exceed <see cref="MaxCapacity"/> or <see cref="MaxCapacityGraphemes"/>.</exception>
        public PostBuilder Append(char value)
        {
            ArgumentNullException.ThrowIfNull(value);

            return Append(value.ToString());
        }

        /// <summary>
        /// Appends a specified number of copies of the string representation of a Unicode character to this instance.
        /// </summary>
        /// <param name="value">The character to append.</param>
        /// <param name="repeatCount">The number of times to append <paramref name="value"/>.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="repeatCount"/> is &lt;=0.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="hashTag"/> is null or its text is null or whitespace.</exception>
        public PostBuilder Append(HashTag hashTag)
        {
            ArgumentNullException.ThrowIfNull(hashTag);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(hashTag.Text);

            lock (_syncLock)
            {
                ByteSlice byteSlice = GetFacetPosition(_postRecord.Text, hashTag.Text);
                _postRecord.Text += hashTag.Text;

                TagFacetFeature tagFacetFeature = new(hashTag.Tag);
                List<FacetFeature> features = new()
                    {
                        tagFacetFeature
                    };

                _postRecord.Facets ??= new List<Facet>();
                _postRecord.Facets.Add(new Facet(byteSlice, features));

                return this;
            }
        }

        /// <summary>
        /// Appends a <see cref="Link"/> to the text and facet features of this instance.
        /// </summary>
        /// <param name="link">The <see cref="Link"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="link"/> is null or its text is null or whitespace.</exception>
        public PostBuilder Append(Link link)
        {
            ArgumentNullException.ThrowIfNull(link);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(link.Text);

            lock (_syncLock)
            {
                ByteSlice byteSlice = GetFacetPosition(_postRecord.Text, link.Text);
                _postRecord.Text += link.Text;

                LinkFacetFeature linkFacetFeature = new(link.Uri);
                List<FacetFeature> features = new()
                    {
                        linkFacetFeature
                    };

                _postRecord.Facets ??= new List<Facet>();
                _postRecord.Facets.Add(new Facet(byteSlice, features));

                return this;
            }
        }

        /// <summary>
        /// Adds a copy of the specified string to the record text to the specified <paramref name="postBuilder"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the string to.</param>
        /// <param name="s">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="link"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="mention"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="hashTag"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if the number of images in this instance is already equal to <see cref="MaxImages"/>.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if the number of images in this instance is already equal to <see cref="MaxImages"/>.</exception>
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
        /// <exception cref="ArgumentOutOfRangeException">if adding the images in this instance is will result in an image count &gt;<see cref="MaxImages"/>.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="images"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if adding the images in this instance is will result in an image count &gt;<see cref="MaxImages"/>.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="video"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="video"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="mention"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="hashTag"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="link"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="image"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="images"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="video"/> is null.</exception>
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

            _postRecord.SetSelfLabels(labels);
        }

        /// <summary>
        /// Converts the value of this instance to a <see cref="Post"/>.
        /// </summary>
        /// <returns>A <see cref="Post"/> whose value is the same as this instance.</returns>
        public Post ToPostRecord()
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
                        _postRecord.EmbeddedRecord = new EmbeddedImages(_embeddedImages);
                    }
                    else if (QuotePost is not null)
                    {
                        // Quote post, so we need fix up the embedded record to include images.
                        _postRecord.EmbeddedRecord = new EmbeddedRecordWithMedia(new EmbeddedRecord(QuotePost), new EmbeddedImages(_embeddedImages));
                    }
                    else
                    {
                        // Reply post
                        _postRecord.EmbeddedRecord = new EmbeddedImages(_embeddedImages);
                    }
                }

                if (HasVideo)
                {
                    if (InReplyTo is null && QuotePost is null)
                    {
                        // Plain old post
                        _postRecord.EmbeddedRecord = _embeddedVideo;
                    }
                    else if (QuotePost is not null)
                    {
                        // Quote post, so we need fix up the embedded record to include the video.
                        _postRecord.EmbeddedRecord = new EmbeddedRecordWithMedia(new EmbeddedRecord(QuotePost), Video);
                    }
                    else
                    {
                        // Reply post
                        _postRecord.EmbeddedRecord = _embeddedVideo;
                    }
                }

                return new Post(_postRecord);
            }
        }

        /// <summary>
        /// Determines whether two object instances are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><see langword="true"/> if the specified object is equal to the current object; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? obj) => obj is PostBuilder builder && EqualityComparer<Post>.Default.Equals(_postRecord, builder._postRecord) && EqualityComparer<List<EmbeddedImage>>.Default.Equals(_embeddedImages, builder._embeddedImages) && EqualityComparer<List<ThreadGateRule>?>.Default.Equals(_threadGateRules, builder._threadGateRules) && EqualityComparer<List<PostGateRule>?>.Default.Equals(_postGateRules, builder._postGateRules) && _disableEmbedding == builder._disableEmbedding;

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
                Post postRecord = new (_postRecord);

                if (_embeddedImages is not null)
                {
                    embeddedImages = new List<EmbeddedImage>(_embeddedImages);
                }

                if (_threadGateRules is not null)
                {
                    threadGateRules = new List<ThreadGateRule>(_threadGateRules);
                }

                if (_postGateRules is not null)
                {
                    postGateRules = new List<PostGateRule>(_postGateRules);
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
