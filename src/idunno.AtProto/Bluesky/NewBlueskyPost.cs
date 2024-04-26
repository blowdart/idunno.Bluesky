// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// A class representing the data needed to create a new Bluesky Post
    /// </summary>
    public class NewBlueskyPost : NewAtProtoRecord
    {
        private const string TextKey = @"text";

        private const string LanguagesKey = @"langs";

        private const string FacetsKey = @"facets";

        private readonly Encoding _utf8 = new UTF8Encoding();

        /// <summary>
        /// Creates a new instance of a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="text"/> for a <see cref="NewBlueskyPost"/> is too long.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="text"/> is null or empty.</exception>
        public NewBlueskyPost(string text) : base(CollectionType.Post)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length > MaximumPostLength)
            {
                throw new ArgumentException($"Post text is longer than {MaximumPostLength} characters", nameof(text));
            }

            Text = text;
        }

        /// <summary>
        /// Creates a new instance of a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="languages">The languages for the post.</param>
        public NewBlueskyPost(string text, string[] languages) : this(text)
        {
            if (languages is not null && languages.Length > 0)
            {
                Languages = languages;
            }
        }

        /// <summary>
        /// Creates a new instance of a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="language">The language for the post.</param>
        public NewBlueskyPost(string text, string language) : this(text)
        {
            if (!string.IsNullOrEmpty(language))
            {
                Languages = new string[] { language };
            }
        }

        /// <summary>
        /// Appends the specified <see cref="Facet"/> to a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="post">The <see cref="NewBlueskyPost" /> to append the facet to.</param>
        /// <param name="facet">The <see cref="Facet"/> to append.</param>
        /// <returns>A <see cref="NewBlueskyPost"/> with the <paramref name="facet"/> appended.</returns>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="text"/> for a <see cref="NewBlueskyPost"/> is too long.</exception>
        /// <exception cref="ArgumentNullException"> Thrown if the supplied post or facet is null.</exception>
        public static NewBlueskyPost operator +(NewBlueskyPost post, Facet facet)
        {
            ArgumentNullException.ThrowIfNull(post);
            ArgumentNullException.ThrowIfNull(facet);

            if ((post.Text.Length + facet.Text.Length) > MaximumPostLength)
            {
                throw new ArgumentException($"Post text would be longer than {MaximumPostLength} characters");
            }

            post.Text += facet.Text;

            if (post.Facets == null)
            {
                post.Facets = new List<Facet> { facet };
            }
            else
            {
                post.Facets.Add(facet);
            }

            return post;
        }

        /// <summary>
        /// Appends the specified <see cref="Facet"/> to a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="post">The <see cref="NewBlueskyPost" /> to append the facet to.</param>
        /// <param name="facet">The <see cref="Facet"/> to append.</param>
        /// <returns>A <see cref="NewBlueskyPost"/> with the <paramref name="facet"/> appended.</returns>
        public static NewBlueskyPost Add(NewBlueskyPost post, Facet facet)
        {
            return post + facet;
        }

        /// <summary>
        /// Appends the specified <see cref="string"/> to a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="post">The <see cref="NewBlueskyPost" /> to append the facet to.</param>
        /// <param name="s">The text to append.</param>
        /// <returns>A <see cref="NewBlueskyPost"/> with the <paramref name="s"/> appended.</returns>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="s"/> for a <see cref="NewBlueskyPost"/> is too long.</exception>
        /// <exception cref="ArgumentNullException"> Thrown if the supplied post or text is null.</exception>
        public static NewBlueskyPost operator +(NewBlueskyPost post, string s)
        {
            ArgumentNullException.ThrowIfNull(post);
            ArgumentNullException.ThrowIfNull(s);

            if ((post.Text.Length + s.Length) > MaximumPostLength)
            {
                throw new ArgumentException($"Post text would be longer than {MaximumPostLength} characters");
            }

            post.Text += s;

            return post;
        }

        /// <summary>
        /// Appends the specified <see cref="string"/> to a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="post">The <see cref="NewBlueskyPost" /> to append the facet to.</param>
        /// <param name="s">The text to append.</param>
        /// <returns>A <see cref="NewBlueskyPost"/> with the <paramref name="s"/> appended.</returns>
        public static NewBlueskyPost Add(NewBlueskyPost post, string s)
        {
            return post + s;
        }

        /// <summary>
        /// Appends the specified <see cref="char"/> to a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="post">The <see cref="NewBlueskyPost" /> to append the facet to.</param>
        /// <param name="c">The <see cref="char"/> to append.</param>
        /// <returns>A <see cref="NewBlueskyPost"/> with the <paramref name="c"/> appended.</returns>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="c"/> for a <see cref="NewBlueskyPost"/> is too long.</exception>
        /// <exception cref="ArgumentNullException"> Thrown if the supplied post is null.</exception>
        public static NewBlueskyPost operator +(NewBlueskyPost post, char c)
        {
            ArgumentNullException.ThrowIfNull(post);

            if ((post.Text.Length + 1) > MaximumPostLength)
            {
                throw new ArgumentException($"Post text would be longer than {MaximumPostLength} characters");
            }

            post.Text += c;

            return post;
        }

        /// <summary>
        /// Appends the specified <see cref="char"/> to a <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <param name="post">The <see cref="NewBlueskyPost" /> to append the facet to.</param>
        /// <param name="c">The <see cref="char"/> to append.</param>
        /// <returns>A <see cref="NewBlueskyPost"/> with the <paramref name="c"/> appended.</returns>
        public static NewBlueskyPost Add(NewBlueskyPost post, char c)
        {
            return post + c;
        }

        /// <summary>
        /// Gets the maximum length of a <see cref="NewBlueskyPost"/>.
        /// </summary>
        [JsonIgnore]
        public static int MaximumPostLength
        {
            get
            {
                return 250;
            }
        }

        /// <summary>
        /// Gets the length of the <see cref="NewBlueskyPost"/> text in UTF8 bytes.
        /// </summary>
        [JsonIgnore]
        public long LengthAsUTF8
        {
            get
            {
                return _utf8.GetByteCount(Text);
            }
        }

        /// <summary>
        /// Gets or sets the text for the <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <value>
        /// The text for the <see cref="NewBlueskyPost"/>.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown on set if the value is null or empty.</exception>
        [JsonIgnore]
        public string Text
        {
            get
            {
                if (!Values.TryGetValue(TextKey, out object? value))
                {
                    return string.Empty;
                }
                else  if (value is not string storedTextObject)
                {
                    return string.Empty;
                }
                else
                {
                    return storedTextObject;
                }
            }

            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (!Values.ContainsKey(TextKey))
                {
                    Values.Add(TextKey, value);
                }
                else
                {
                    Values[TextKey] = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the languages for the <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <value>
        /// The optional languages for the <see cref="NewBlueskyPost"/>.
        /// </value>
        [JsonIgnore]
        public IList<string>? Languages
        {
            get
            {
                if (!Values.TryGetValue(LanguagesKey, out object? value))
                {
                    return null;
                }
                else
                {
                    return value as IList<string>;
                }
            }

            set
            {
                if (value is not null)
                {
                    if (!Values.ContainsKey(LanguagesKey))
                    {
                        Values.Add(LanguagesKey, value);
                    }
                    else
                    {
                        Values[LanguagesKey] = value;
                    }
                }
                else
                {
                    if (Values.ContainsKey(LanguagesKey))
                    {
                        Values.Remove(LanguagesKey);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the facets for the <see cref="NewBlueskyPost"/>.
        /// </summary>
        /// <value>
        /// The optional facets for the <see cref="NewBlueskyPost"/>.
        /// </value>
        [JsonIgnore]
        public IList<Facet>? Facets
        {
            get
            {
                if (!Values.TryGetValue(FacetsKey, out object? value))
                {
                    return null;
                }
                else
                {
                    return value as List<Facet>;
                }
            }

            set
            {
                if (value == null)
                {
                    if (Values.ContainsKey(FacetsKey))
                    {
                        Values.Remove(FacetsKey);
                    }
                }
                else
                {
                    Values[FacetsKey] = value;
                }
            }
        }
    }
}
