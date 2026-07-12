// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.RichText;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public sealed partial class PostBuilder
{

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashTag"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hashTag"/>'s text is <see langword="null"/> or whitespace.</exception>
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
    /// <exception cref="ArgumentNullException">Thrown when  <paramref name="link"/> is <see langword="null"/> or its text is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="link"/>'s is <see langword="null"/> or whitespace.</exception>
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
    /// Appends a <see cref="Mention"/> to the text and and facet features of this instance.
    /// </summary>
    /// <param name="mention">The <see cref="Mention"/> to append.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mention"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="mention"/>'s text is <see langword="null"/> or white space.</exception>
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
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    string.Format(null, s_postTextExceedsMaxLength, MaxCapacity, MaxCapacityGraphemes));
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
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    string.Format(null, s_postTextExceedsMaxLength, MaxCapacity, MaxCapacityGraphemes));
            }
            else
            {
                _post.Text += value;
                return this;
            }
        }
    }
}