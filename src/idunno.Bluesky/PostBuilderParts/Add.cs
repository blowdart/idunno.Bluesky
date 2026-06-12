// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public sealed partial class PostBuilder
{
    /// <summary>
    /// Adds a copy of the specified string to the record text of this instance.
    /// </summary>
    /// <param name="s">The string to append</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    public PostBuilder Add(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        return this + s;
    }

    /// <summary>
    /// Adds a copy of the specified string to the record text to the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the string to.</param>
    /// <param name="s">The string to append</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is <see langword="null"/>.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, string s)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);

        return postBuilder + s;
    }

    /// <summary>
    /// Adds a copy of the specified character to the post text of this instance.
    /// </summary>
    /// <param name="c">The character to append</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public PostBuilder Add(char c)
    {
        return Append(c, 1);
    }

    /// <summary>
    /// Adds a the specified character to the post text of the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the character to.</param>
    /// <param name="c">The character to append</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is <see langword="null"/>.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, char c)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        return postBuilder.Append(c, 1);
    }


    /// <summary>
    /// Adds the specified character, repeated by the specified count, to the post text of this instance.
    /// </summary>
    /// <param name="c">The character to append</param>
    /// <param name="repeatCount">The number of times to repeat the <paramref name="c" />.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public PostBuilder Add(char c, int repeatCount)
    {
        return Append(c, repeatCount);
    }

    /// <summary>
    /// Adds a copy of the specified character to the record text to the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the string to.</param>
    /// <param name="c">The character to append</param>
    /// <param name="repeatCount">The number of times to repeat the <paramref name="c" />.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is <see langword="null"/>.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, char c, int repeatCount)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);

        return postBuilder.Append(c, repeatCount);
    }

    /// <summary>
    /// Adds a <see cref="Link"/> to the text and facet features of this instance.
    /// </summary>
    /// <param name="link">The <see cref="Link"/> to add.</param>
    /// <returns>A reference to this instance after the add operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="link"/> is <see langword="null"/>.</exception>
    public PostBuilder Add(Link link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return Append(link);
    }

    /// <summary>
    /// Adds a <see cref="Link"/> to the text and facet features of the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the link to.</param>
    /// <param name="link">The <see cref="Link"/> to add.</param>
    /// <returns>A reference to this instance after the add operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="link"/> is <see langword="null"/>.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, Link link)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(link);

        return postBuilder.Append(link);
    }

    /// <summary>
    /// Adds a <see cref="Mention"/> to the text and and facet features to this instance.
    /// </summary>
    /// <param name="mention">The <see cref="Mention"/> to add.</param>
    /// <returns>A reference to this instance after the add operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mention"/> is <see langword="null"/>.</exception>
    public PostBuilder Add(Mention mention)
    {
        ArgumentNullException.ThrowIfNull(mention);

        return Append(mention);
    }

    /// <summary>
    /// Adds a <see cref="Mention"/> to the text and and facet features to the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the mention to.</param>
    /// <param name="mention">The <see cref="Mention"/> to add.</param>
    /// <returns>A reference to this instance after the add operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="mention"/> is <see langword="null"/>.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, Mention mention)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(mention);

        return postBuilder.Append(mention);
    }

    /// <summary>
    /// Adds a <see cref="HashTag"/> to the text and facet features to this instance.
    /// </summary>
    /// <param name="hashTag">The <see cref="HashTag"/> to add.</param>
    /// <returns>A reference to this instance after the add operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashTag"/> is <see langword="null"/>.</exception>
    public PostBuilder Add(HashTag hashTag)
    {
        ArgumentNullException.ThrowIfNull(hashTag);

        return Append(hashTag);
    }

    /// <summary>
    /// Adds a <see cref="HashTag"/> to the text and facet features to the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the hash tag to.</param>
    /// <param name="hashTag">The <see cref="HashTag"/> to add.</param>
    /// <returns>A reference to this instance after the add operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="hashTag"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException">if <paramref name="images"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="images"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the this instance already has images.</exception>
    public PostBuilder Add(EmbeddedVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        lock (_syncLock)
        {
            if (_embeddedImages.Count > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(video), Properties.Resources.CannotAddVideoToPostWithImages);
            }

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
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="postBuilder" /> already has images.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, EmbeddedVideo video)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(video);

        lock (postBuilder._syncLock)
        {
            if (postBuilder._embeddedImages.Count > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(video), Properties.Resources.CannotAddVideoToPostWithImages);
            }

            postBuilder.Video = video;
            postBuilder._embeddedImages.Clear();
            return postBuilder;
        }
    }

    /// <summary>
    /// Adds an embedded record pointing to the ATProto record specified by <paramref name="strongReference"/> to this instance.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference"/> to embed.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    public PostBuilder Add(StrongReference strongReference)
    {
        ArgumentNullException.ThrowIfNull(strongReference);
        QuotePost = strongReference;
        return this;
    }

    /// <summary>
    /// Adds an embedded record pointing to the ATProto record specified by <paramref name="strongReference"/> to the specified <paramref name="postBuilder" />.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the strong reference to.</param>
    /// <param name="strongReference">The <see cref="StrongReference"/> to embed.</param>
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, StrongReference strongReference)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(strongReference);
        postBuilder.QuotePost = strongReference;
        return postBuilder;
    }

    /// <summary>
    /// Adds a <see cref="SelfLabel"/> to this instance.
    /// </summary>
    /// <param name="selfLabel">The <see cref="SelfLabel"/> to add.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selfLabel"/> or the label's value is <see langword="null"/>.</exception>
    public PostBuilder Add(SelfLabel selfLabel)
    {
        ArgumentNullException.ThrowIfNull(selfLabel);
        ArgumentNullException.ThrowIfNull(selfLabel.Value);

        lock (_syncLock)
        {
            _post.Labels ??= new SelfLabels();
            _post.Labels.AddLabel(selfLabel);
        }

        return this;
    }

    /// <summary>
    /// Adds a <see cref="SelfLabel"/> to this instance.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the label to.</param>
    /// <param name="selfLabel">The <see cref="SelfLabel"/> to add.</param>
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/>, <paramref name="selfLabel"/>, or the label's value is <see langword="null"/>.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, SelfLabel selfLabel)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(selfLabel);
        ArgumentNullException.ThrowIfNull(selfLabel.Value);

        postBuilder.Add(selfLabel);

        return postBuilder;
    }

    /// <summary>
    /// Adds a <see cref="EmbeddedGalleryImage"/> to this instance.
    /// </summary>
    /// <param name="image">The <see cref="EmbeddedGalleryImage"/> to add.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the maximum number of gallery items is exceeded or a video is already present.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    public PostBuilder Add(EmbeddedGalleryImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        lock (_syncLock)
        {
            if (_embeddedVideo is not null)
            {
                throw new ArgumentOutOfRangeException(nameof(image), Properties.Resources.PostBuilderCannotHaveImagesAndGalleryImages);
            }
            if (_embeddedGalleryImages.Count >= MaxGalleryItems)
            {
                throw new ArgumentOutOfRangeException(nameof(image), $"cannot have more than {MaxGalleryItems} in a post.");
            }
            _embeddedGalleryImages.Add(image);
        }
        return this;
    }

    /// <summary>
    /// Adds a <see cref="EmbeddedGalleryImage"/> to the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the image to.</param>
    /// <param name="image">The <see cref="EmbeddedGalleryImage"/> to add.</param>
    /// <returns>The <paramref name="postBuilder"/> after the append operation has completed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the maximum number of gallery items is exceeded or a video is already present.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    public static PostBuilder Add(PostBuilder postBuilder, EmbeddedGalleryImage image)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(image);
        lock (postBuilder._syncLock)
        {
            if (postBuilder._embeddedVideo is not null)
            {
                throw new ArgumentOutOfRangeException(nameof(image), Properties.Resources.PostBuilderCannotHaveImagesAndGalleryImages);
            }
            if (postBuilder._embeddedGalleryImages.Count >= postBuilder.MaxGalleryItems)
            {
                throw new ArgumentOutOfRangeException(nameof(image), $"cannot have more than {postBuilder.MaxGalleryItems} in a post.");
            }
            postBuilder._embeddedGalleryImages.Add(image);
        }
        return postBuilder;
    }


    /// <summary>
    /// Adds a copy of the specified string to the record text of the specified <paramref name="postBuilder" />.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the string to.</param>
    /// <param name="s">The string to append</param>
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is <see langword="null"/>.</exception>
    public static PostBuilder operator +(PostBuilder postBuilder, string s)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);

        return postBuilder.Append(s);
    }

    /// <summary>
    /// Adds the specified character to the record text of the specified <paramref name="postBuilder" />.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the character to.</param>
    /// <param name="c">The character to append</param>
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is <see langword="null"/>.</exception>
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
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="mention"/> is <see langword="null"/>.</exception>
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
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="hashTag"/> is <see langword="null"/>.</exception>
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
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="link"/> is <see langword="null"/>.</exception>
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
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
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
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="images"/> is <see langword="null"/>.</exception>
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
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="video"/> is <see langword="null"/>.</exception>
    public static PostBuilder operator +(PostBuilder postBuilder, EmbeddedVideo video)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(video);

        return Add(postBuilder, video);
    }

    /// <summary>
    /// Adds an embedded record pointing to the ATProto record specified by <paramref name="strongReference"/> to the specified <paramref name="postBuilder" />.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the strong reference to.</param>
    /// <param name="strongReference">The <see cref="StrongReference"/> to embed.</param>
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    public static PostBuilder operator +(PostBuilder postBuilder, StrongReference strongReference)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(strongReference);
        return Add(postBuilder, strongReference);
    }

    /// <summary>
    /// Adds a <paramref name="selfLabel"/> to the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the self label to.</param>
    /// <param name="selfLabel">The <see cref="SelfLabel"/> to add.</param>
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/>, <paramref name="selfLabel"/>, or the label's value is <see langword="null"/>.</exception>
    public static PostBuilder operator +(PostBuilder postBuilder, SelfLabel selfLabel)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(selfLabel);
        ArgumentNullException.ThrowIfNull(selfLabel.Value);
        return Add(postBuilder, selfLabel);
    }

    /// <summary>
    /// Adds an <see cref="EmbeddedGalleryImage"/> to the specified <paramref name="postBuilder" />.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to add the image to.</param>
    /// <param name="image">The <see cref="EmbeddedGalleryImage"/> to add.</param>
    /// <returns>The <paramref name="postBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    public static PostBuilder operator +(PostBuilder postBuilder, EmbeddedGalleryImage image)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);
        ArgumentNullException.ThrowIfNull(image);
        return Add(postBuilder, image);
    }
}