// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Repo;
using idunno.Bluesky.Actions.Model;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky
{
    // TODO: ThreadGate & PostGate, and the appropriate Post method

    /// <summary>
    /// A class to allow building of a Post record in a more friendly manner.
    /// </summary>
    public sealed class PostBuilder
    {
        private readonly NewPostRecord _postRecord;
        private readonly List<EmbeddedImage> _embeddedImages = new ();
        private List<ThreadGateRule>? _threadGateRules;
        private List<PostGateRule>? _postGateRules ;
        private bool _disableReplies;
        private bool _disableEmbedding;

        /// <summary>
        /// Creates a new instance of a <see cref="PostBuilder"/>.
        /// </summary>
        public PostBuilder()
        {
            _postRecord = new NewPostRecord() { CreatedAt = DateTimeOffset.UtcNow };
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PostBuilder"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="images">A collection of <see cref="EmbeddedImage"/>s to attach to the post, if any.</param>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="text"/> for a <see cref="PostBuilder"/> is too long.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="text"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If the text for the post is too long or too many images are specified.</exception>
        public PostBuilder(string text, ICollection<EmbeddedImage>? images = null) : this()
        {
            ArgumentNullException.ThrowIfNull(text);

            if (text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
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
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PostBuilder"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="language">The language for the post.</param>
        public PostBuilder(string text, string language) : this(text, languages: new string[] { language }, images: null)
        {
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PostBuilder"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="languages">The languages for the post.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="languages"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="languages"/> contains no entries.</exception>
        public PostBuilder(string text, string[] languages, ICollection<EmbeddedImage>? images = null) : this(text, images)
        {
            ArgumentNullException.ThrowIfNull(languages);
            ArgumentOutOfRangeException.ThrowIfZero(languages.Length, nameof(languages));

            _postRecord.Langs = languages;
        }


        /// <summary>
        /// Gets a flag indicating whether this instance has any record text.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Length))]
        public bool HasText => _postRecord.Text is not null && !string.IsNullOrEmpty(_postRecord.Text);

        /// <summary>
        /// Gets the length of the post text, if any.
        /// </summary>
        public int? Length
        {
            get
            {
                lock (_postRecord)
                {
                    return _postRecord.Text?.Length;
                }
            }
        }

        /// <summary>
        /// Gets the maximum capacity of characters allowed in the record text of this instance
        /// </summary>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Matching StringBuilder property.")]
        public int MaxCapacity => Maximum.PostLengthInCharacters;

        /// <summary>
        /// Gets the maximum capacity of graphemes allowed in the record text of this instance
        /// </summary>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Matching StringBuilder property.")]
        public int MaxCapacityGraphemes => Maximum.PostLengthInGraphemes;

        /// <summary>
        /// Gets the maximum capacity of images allowed in this instance
        /// </summary>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Matching StringBuilder property.")]
        public int MaxImages => Maximum.ImagesInPost;

        /// <summary>
        /// Gets a copy of the text for this instance.
        /// </summary>
        public string? Text
        {
            get
            {
                lock (_postRecord)
                {
                    return _postRecord.Text;
                }
            }

            internal set
            {
                if (value is not null)
                {
                    if (value.Length > Maximum.PostLengthInCharacters || value.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
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
                lock (_postRecord)
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

        /// <summary>
        /// Gets a flag indicating whether this instance has any images
        /// </summary>
        [MemberNotNullWhen(true, nameof(Images))]
        public bool HasImages => _embeddedImages is not null && _embeddedImages.Count > 0;

        /// <summary>
        /// Gets a readonly list of the images for the post.
        /// </summary>
        public IReadOnlyList<EmbeddedImage>? Images
        {
            get
            {
                lock (_embeddedImages)
                {
                    return new List<EmbeddedImage>(_embeddedImages).AsReadOnly();
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
                lock (_postRecord)
                {
                    return _postRecord.Reply;
                }
            }

            set
            {
                lock (_postRecord)
                {
                    if (_threadGateRules is not null || DisableReplies is true)
                    {
                        throw new ArgumentException("Cannot set InReplyTo if ThreadGateRules is not null.");
                    }

                    _postRecord.Reply = value;

                    if (value is not null)
                    {
                        if (_postRecord.Embed is EmbeddedRecord || _postRecord.Embed is EmbeddedRecordWithMedia)
                        {
                            // Being a reply post excludes being a quote post.
                            // Quote = null;
                            _postRecord.Embed = null;
                        }
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
                lock (_postRecord)
                {
                    if (_postRecord.Embed is EmbeddedRecord embeddedRecord)
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
                lock (_postRecord)
                {
                    if (value is null && _postRecord.Embed is null)
                    {
                        // Nothing to do
                        return;
                    }
                    else if (value is null)
                    {
                        if (_postRecord.Embed is EmbeddedRecord || _postRecord.Embed is EmbeddedRecordWithMedia)
                        {
                            /// We already have a quote record, so let's just delete it.
                            _postRecord.Embed = null;
                        }
                        else
                        {
                            throw new ArgumentException("postRecord has embed value other than an EmbeddedRecord or EmbeddedRecordWithMedia");
                        }
                    }
                    else
                    {
                        _postRecord.Embed = new EmbeddedRecord(value);
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
                lock (_postRecord)
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
                lock (_postRecord)
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
                lock (_postRecord)
                {
                    _disableEmbedding = value;

                    if (_disableEmbedding)
                    {

                        bool needToAddToRules = true;

                        if (_postGateRules != null)
                        {
                            foreach (PostGateRule rule in _postGateRules)
                            {
                                if (rule is DisableEmbeddingRule)
                                {
                                    needToAddToRules = false;
                                    break;
                                }
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
                            foreach (PostGateRule rule in _postGateRules)
                            {
                                if (rule is DisableEmbeddingRule)
                                {
                                    _postGateRules.Remove(rule);
                                    break;
                                }
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
                lock (_postRecord)
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
                        foreach (PostGateRule rule in _postGateRules)
                        {
                            if (rule is DisableEmbeddingRule)
                            {
                                _disableReplies = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Appends a copy of the specified string to the record text of this instance.
        /// </summary>
        /// <param name="value">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the the record text of this instance would exceed <see cref="MaxCapacity"/> or <see cref="MaxCapacityGraphemes"/>.</exception>
        public PostBuilder Append(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return this;
            }

            lock (_postRecord)
            {
                if (value.Length > MaxCapacity || value.GetLengthInGraphemes() > MaxCapacityGraphemes)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"string cannot have a length greater than {MaxCapacity} characters, or {MaxCapacityGraphemes} graphemes.");
                }

                if (_postRecord.Text is null)
                {
                    _postRecord.Text = value;
                    return this;
                }

                if ((value.Length + _postRecord.Text.Length > MaxCapacity) ||
                    (value.GetLengthInGraphemes() + _postRecord.Text.GetLengthInGraphemes() > MaxCapacityGraphemes))
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
        /// Adds a copy of the specified string to the record text of this instance.
        /// </summary>
        /// <param name="s">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, string s)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            return postBuilder + s;
        }

        /// <summary>
        /// Adds a copy of the specified string to the record text of this instance.
        /// </summary>
        /// <param name="s">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, string s)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            return postBuilder.Append(s);
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
        /// Adds a copy of the specified character to the record text of this instance.
        /// </summary>
        /// <param name="c">The string to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, char c, int repeatCount = 1)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            return postBuilder.Append(c, repeatCount);
        }

        /// <summary>
        /// Adds a copy of the specified string to the record text of this instance.
        /// </summary>
        /// <param name="c">The character to append</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, char c)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            return postBuilder.Append(c);
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

            lock (_postRecord)
            {
                long startingPosition = 0;
                if (_postRecord.Text is not null)
                {
                    startingPosition = _postRecord.Text.Length;
                }

                _postRecord.Text += mention.Text;

                ByteSlice byteSlice = new(startingPosition, startingPosition + mention.Text.GetUtf8Length());
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
        /// Adds a <see cref="Mention"/> to the text and and facet features of this instance.
        /// </summary>
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
        /// Adds a <see cref="Mention"/> to the text and facet features of this instance.
        /// </summary>
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
        /// Appends a <see cref="HashTag"/> to the text and facet features of this instance.
        /// </summary>
        /// <param name="hashTag">The <see cref="HashTag"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="hashTag"/> is null or its text is null or whitespace.</exception>
        public PostBuilder Append(HashTag hashTag)
        {
            ArgumentNullException.ThrowIfNull(hashTag);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(hashTag.Text);

            lock (_postRecord)
            {
                long startingPosition = 0;
                if (_postRecord.Text is not null)
                {
                    startingPosition = _postRecord.Text.Length;
                }
                _postRecord.Text += hashTag.Text;

                ByteSlice byteSlice = new(startingPosition, startingPosition + hashTag.Text.GetUtf8Length());
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
        /// Adds a <see cref="HashTag"/> to the text and facet features of this instance.
        /// </summary>
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
        /// Adds a <see cref="HashTag"/> to the text and facet features of this instance.
        /// </summary>
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
        /// Appends a <see cref="Link"/> to the text and facet features of this instance.
        /// </summary>
        /// <param name="link">The <see cref="Link"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="link"/> is null or its text is null or whitespace.</exception>
        public PostBuilder Append(Link link)
        {
            ArgumentNullException.ThrowIfNull(link);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(link.Text);

            lock (_postRecord)
            {
                long startingPosition = 0;
                if (_postRecord.Text is not null)
                {
                    startingPosition = _postRecord.Text.Length;
                }
                _postRecord.Text += link.Text;

                ByteSlice byteSlice = new(startingPosition, startingPosition + link.Text.GetUtf8Length());
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
        /// Adds a <see cref="Link"/> to the text and facet features of this instance.
        /// </summary>
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
        /// Adds a <see cref="HashTag"/> to the text and facet features of this instance.
        /// </summary>
        /// <param name="hashTag">The <see cref="HashTag"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="hashTag"/> is null.</exception>
        public static PostBuilder operator +(PostBuilder postBuilder, Link link)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(link);

            return postBuilder.Append(link);
        }

        /// <summary>
        /// Adds a <see cref="EmbeddedImage"/> to this instance.
        /// </summary>
        /// <param name="image">The <see cref="EmbeddedImage"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if the number of images in this instance is already equal to <see cref="MaxImages"/>.</exception>
        public PostBuilder Add(EmbeddedImage image)
        {
            ArgumentNullException.ThrowIfNull(image);

            lock (_postRecord)
            {
                if (_embeddedImages.Count == MaxImages)
                {
                    throw new ArgumentOutOfRangeException(nameof(image), $"cannot have more than {MaxImages} in a post.");
                }

                _embeddedImages.Add(image);
                return this;
            }
        }

        /// <summary>
        /// Adds a <see cref="EmbeddedImage"/> to this instance.
        /// </summary>
        /// <param name="image">The <see cref="EmbeddedImage"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if the number of images in this instance is already equal to <see cref="MaxImages"/>.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, EmbeddedImage image)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(image);

            lock (postBuilder._postRecord)
            {
                if (postBuilder._embeddedImages.Count == postBuilder.MaxImages)
                {
                    throw new ArgumentOutOfRangeException(nameof(image), $"cannot have more than {postBuilder.MaxImages} in a post.");
                }

                postBuilder._embeddedImages.Add(image);
                return postBuilder;
            }
        }

        /// <summary>
        /// Adds a <see cref="EmbeddedImage"/> to this instance.
        /// </summary>
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
        /// Adds a collection <see cref="EmbeddedImage"/>s to this instance.
        /// </summary>
        /// <param name="images">The collection of <see cref="EmbeddedImage"/> to append.</param>
        /// <returns>A reference to this instance after the append operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if adding the images in this instance is will result in an image count &gt;<see cref="MaxImages"/>.</exception>
        public PostBuilder Add(ICollection<EmbeddedImage> images)
        {
            ArgumentNullException.ThrowIfNull(images);

            lock (_postRecord)
            {
                if ((_embeddedImages.Count + images.Count) > MaxImages)
                {
                    throw new ArgumentOutOfRangeException(nameof(images), $"cannot have more than {MaxImages} in a post.");
                }

                _embeddedImages.AddRange(images);
                return this;
            }
        }

        /// <summary>
        /// Adds a collection of <see cref="EmbeddedImage"/>s to this instance.
        /// </summary>
        /// <param name="images">The collection of <see cref="EmbeddedImages"/> to add.</param>
        /// <returns>A reference to this instance after the add operation has completed.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> or <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if adding the images in this instance is will result in an image count &gt;<see cref="MaxImages"/>.</exception>
        public static PostBuilder Add(PostBuilder postBuilder, ICollection<EmbeddedImage> images)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);
            ArgumentNullException.ThrowIfNull(images);

            lock (postBuilder._postRecord)
            {
                if ((postBuilder._embeddedImages.Count + images.Count) > postBuilder.MaxImages)
                {
                    throw new ArgumentOutOfRangeException(nameof(images), $"cannot have more than {postBuilder.MaxImages} in a post.");
                }

                postBuilder._embeddedImages.AddRange(images);
                return postBuilder;
            }
        }

        /// <summary>
        /// Adds a collection of <see cref="EmbeddedImage"/>s to this instance.
        /// </summary>
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
        /// Converts the value of this instance to a <see cref="NewPostRecord"/>.
        /// </summary>
        /// <returns>A <see cref="NewPostRecord"/> whose value is the same as this instance.</returns>
        public NewPostRecord ToPostRecord()
        {
            lock (_postRecord)
            {
                if (!HasText)
                {
                    throw new PostBuilderException("Post text cannot be null or empty.");
                }

                if (InReplyTo is not null && QuotePost is not null)
                {
                    throw new PostBuilderException("Post cannot be both a reply and a quote");
                }

                if (HasImages)
                {
                    if (InReplyTo is null && QuotePost is null)
                    {
                        // Plain old post
                        _postRecord.Embed = new EmbeddedImages(_embeddedImages);
                    }
                    else if (InReplyTo is not null && QuotePost is null)
                    {
                        // Reply post
                        _postRecord.Embed = new EmbeddedImages(_embeddedImages);
                    }
                    else if (QuotePost is not null && InReplyTo is null)
                    {
                        // Quote post, so we need fix up the embedded record to include images.
                        _postRecord.Embed = new EmbeddedRecordWithMedia(QuotePost, new EmbeddedImages(_embeddedImages));
                    }
                }

                return new NewPostRecord(_postRecord);
            }
        }
    }
}
