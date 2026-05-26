// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using idunno.AtProto;
using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Standard.Site;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// standard.site record encapsulating what content contains.
/// </summary>
/// <remarks>
/// <para>See <see href="https://standard.site"/></para>
/// </remarks>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType, IgnoreUnrecognizedTypeDiscriminators = true)]
[JsonDerivedType(typeof(Document), "site.standard.document")]
public record Document : Document<JsonNode>
{
    /// <summary>
    /// Creates a new <see cref="Document"/> encapsulating what content contains, where content is mapped to a <see cref="JsonNode"/>.
    /// </summary>
    /// <param name="site">Points to a publication record (at://) or a publication url (https://) for loose documents. Avoid trailing slashes.</param>
    /// <param name="title">Title of the document.</param>
    /// <param name="publishedAt">Timestamp of the documents publish time.</param>
    /// <param name="path">Optional path that is combined with site or publication url to construct a canonical URL to the document. Prepend with a leading slash.</param>
    /// <param name="description">Optional brief description or excerpt from the document.</param>
    /// <param name="coverImage">Optional image to used for thumbnail or cover image. Size must be less than 1MB.</param>
    /// <param name="content">Optional open union used to define the record's content. Each entry must specify a $type and may be extended with other lexicons to support additional content formats.</param>
    /// <param name="textContent">Plaintext representation of the documents contents. Should not contain markdown or other formatting.</param>
    /// <param name="bskyPostRef">Strong reference to a Bluesky post. Useful to keep track of comments off-platform.</param>
    /// <param name="tags">Array of strings used to tag or categorize the document. Avoid prepending tags with hashtags.</param>
    /// <param name="updatedAt">Timestamp of the documents last edit, if any.</param>
    /// <remarks>
    /// <para>
    /// <paramref name="content"/> is defined in the lexicon as an unconstrained union, meaning its type cannot be determined.
    /// It is expected that developers will prefer the generic version of this class to provide a strongly typed content property.
    /// </para>
    /// <para>See <see href="https://standard.site"/></para>
    /// </remarks>
    [JsonConstructor]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "They're all various overloads for site types.")]
    public Document(
        string site,
        string title,
        DateTimeOffset publishedAt,
        string? path = null,
        string? description = null,
        Blob? coverImage = null,
        JsonNode? content = null,
        string? textContent = null,
        StrongReference? bskyPostRef = null,
        IEnumerable<string>? tags = null,
        DateTimeOffset? updatedAt = null) : base (
            site: site,
            path: path,
            publishedAt: publishedAt,
            title: title,
            description: description,
            coverImage: coverImage,
            content: content,
            textContent: textContent,
            bskyPostRef: bskyPostRef,
            tags: tags,
            updatedAt: updatedAt)
    {
    }

    /// <summary>
    /// Creates a new <see cref="Document"/> encapsulating what content contains, where content is mapped to a <see cref="JsonNode"/>.
    /// </summary>
    /// <param name="site">Points a publication url (https://) for loose documents.</param>
    /// <param name="title">Title of the document.</param>
    /// <param name="publishedAt">Timestamp of the documents publish time.</param>
    /// <param name="path">Optional path that is combined with site or publication url to construct a canonical URL to the document. Prepend with a leading slash.</param>
    /// <param name="description">Optional brief description or excerpt from the document.</param>
    /// <param name="coverImage">Optional image to used for thumbnail or cover image. Size must be less than 1MB.</param>
    /// <param name="content">Optional open union used to define the record's content. Each entry must specify a $type and may be extended with other lexicons to support additional content formats.</param>
    /// <param name="textContent">Plaintext representation of the documents contents. Should not contain markdown or other formatting.</param>
    /// <param name="bskyPostRef">Strong reference to a Bluesky post. Useful to keep track of comments off-platform.</param>
    /// <param name="tags">Array of strings used to tag or categorize the document. Avoid prepending tags with hashtags.</param>
    /// <param name="updatedAt">Timestamp of the documents last edit, if any.</param>
    /// <remarks>
    /// <para>
    /// <paramref name="content"/> is defined in the lexicon as an unconstrained union, meaning its type cannot be determined.
    /// It is expected that developers will prefer the generic version of this class to provide a strongly typed content property.
    /// </para>
    /// <para>See <see href="https://standard.site"/></para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "They're all various overloads for site types.")]
    public Document(
        Uri site,
        string title,
        DateTimeOffset publishedAt,
        string? path = null,
        string? description = null,
        Blob? coverImage = null,
        JsonNode? content = null,
        string? textContent = null,
        StrongReference? bskyPostRef = null,
        IEnumerable<string>? tags = null,
        DateTimeOffset? updatedAt = null) : base(
            site: site,
            path: path,
            publishedAt: publishedAt,
            title: title,
            description: description,
            coverImage: coverImage,
            content: content,
            textContent: textContent,
            bskyPostRef: bskyPostRef,
            tags: tags,
            updatedAt: updatedAt)
    {
    }

    /// <summary>
    /// Creates a new <see cref="Document"/> encapsulating what content contains, where content is mapped to a <see cref="JsonNode"/>.
    /// </summary>
    /// <param name="site">Points to a publication record (at://) for loose documents.</param>
    /// <param name="title">Title of the document.</param>
    /// <param name="publishedAt">Timestamp of the documents publish time.</param>
    /// <param name="path">Optional path that is combined with site or publication url to construct a canonical URL to the document. Prepend with a leading slash.</param>
    /// <param name="description">Optional brief description or excerpt from the document.</param>
    /// <param name="coverImage">Optional image to used for thumbnail or cover image. Size must be less than 1MB.</param>
    /// <param name="content">Optional open union used to define the record's content. Each entry must specify a $type and may be extended with other lexicons to support additional content formats.</param>
    /// <param name="textContent">Plaintext representation of the documents contents. Should not contain markdown or other formatting.</param>
    /// <param name="bskyPostRef">Strong reference to a Bluesky post. Useful to keep track of comments off-platform.</param>
    /// <param name="tags">Array of strings used to tag or categorize the document. Avoid prepending tags with hashtags.</param>
    /// <param name="updatedAt">Timestamp of the documents last edit, if any.</param>
    /// <remarks>
    /// <para>
    /// <paramref name="content"/> is defined in the lexicon as an unconstrained union, meaning its type cannot be determined.
    /// It is expected that developers will prefer the generic version of this class to provide a strongly typed content property.
    /// </para>
    /// <para>See <see href="https://standard.site"/></para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "They're all various overloads for site types.")]
    public Document(
        AtUri site,
        string title,
        DateTimeOffset publishedAt,
        string? path = null,
        string? description = null,
        Blob? coverImage = null,
        JsonNode? content = null,
        string? textContent = null,
        StrongReference? bskyPostRef = null,
        IEnumerable<string>? tags = null,
        DateTimeOffset? updatedAt = null) : base(
            site: site,
            path: path,
            title: title,
            publishedAt: publishedAt,
            description: description,
            coverImage: coverImage,
            content: content,
            textContent: textContent,
            bskyPostRef: bskyPostRef,
            tags: tags,
            updatedAt: updatedAt)
    {
    }
}

/// <summary>
/// standard.site record encapsulating what content contains.
/// </summary>
/// <typeparam name="T">The type of the content.</typeparam>
/// <remarks>
/// <para>See <see href="https://standard.site"/></para>
/// </remarks>
public record Document<T> : AtProtoRecord where T : class
{
    /// <summary>
    /// Creates a new <see cref="Document{T}"/> encapsulating what content contains.
    /// </summary>
    /// <param name="site">Points to a publication record (at://) or a publication url (https://) for loose documents. Avoid trailing slashes.</param>
    /// <param name="title">Title of the document.</param>
    /// <param name="publishedAt">Timestamp of the documents publish time.</param>
    /// <param name="path">Optional path that is combined with site or publication url to construct a canonical URL to the document. Prepend with a leading slash.</param>
    /// <param name="description">Optional brief description or excerpt from the document.</param>
    /// <param name="coverImage">Optional image to used for thumbnail or cover image. Size must be less than 1MB.</param>
    /// <param name="content">Optional open union used to define the record's content. Each entry must specify a $type and may be extended with other lexicons to support additional content formats.</param>
    /// <param name="textContent">Plaintext representation of the documents contents. Should not contain markdown or other formatting.</param>
    /// <param name="bskyPostRef">Strong reference to a Bluesky post. Useful to keep track of comments off-platform.</param>
    /// <param name="tags">Array of strings used to tag or categorize the document. Avoid prepending tags with hashtags.</param>
    /// <param name="updatedAt">Timestamp of the documents last edit, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="site"/> or <paramref name="title"/> is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="content"/> is defined in the lexicon as an unconstrained union, meaning its type cannot be determined. It is expected that instances of this
    /// class provide a customized class or record to match their specific requirements. If using AOT you must also ensure you capture the type in a <see cref="JsonSerializerContext"/>.
    /// </para>
    /// <para>See <see href="https://standard.site"/></para>
    /// </remarks>
    [JsonConstructor]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "They're all various overloads for site types.")]
    public Document(
        string site,
        string title,
        DateTimeOffset publishedAt,
        string? path = null,
        string? description = null,
        Blob? coverImage = null,
        T? content = null,
        string? textContent = null,
        StrongReference? bskyPostRef = null,
        IEnumerable<string>? tags = null,
        DateTimeOffset? updatedAt = null) 
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(title);

        Site = site;
        Title = title;
        PublishedAt = publishedAt;

        Path = path;
        Description = description;
        CoverImage = coverImage;
        Content = content;
        TextContent = textContent;
        BSkyPostRef = bskyPostRef;

        if (tags is not null)
        {
            Tags = tags;
        }

        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a new <see cref="Document{T}"/> encapsulating what content contains.
    /// </summary>
    /// <param name="site">The publication <see cref="Uri"/> for loose documents.</param>
    /// <param name="title">Title of the document.</param>
    /// <param name="publishedAt">Timestamp of the documents publish time.</param>
    /// <param name="path">Optional path that is combined with site or publication url to construct a canonical URL to the document. Prepend with a leading slash.</param>
    /// <param name="description">Optional brief description or excerpt from the document.</param>
    /// <param name="coverImage">Optional image to used for thumbnail or cover image. Size must be less than 1MB.</param>
    /// <param name="content">Optional open union used to define the record's content. Each entry must specify a $type and may be extended with other lexicons to support additional content formats.</param>
    /// <param name="textContent">Plaintext representation of the documents contents. Should not contain markdown or other formatting.</param>
    /// <param name="bskyPostRef">Strong reference to a Bluesky post. Useful to keep track of comments off-platform.</param>
    /// <param name="tags">Array of strings used to tag or categorize the document. Avoid prepending tags with hashtags.</param>
    /// <param name="updatedAt">Timestamp of the documents last edit, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="site"/> or <paramref name="title"/> is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="content"/> is defined in the lexicon as an unconstrained union, meaning its type cannot be determined. It is expected that instances of this
    /// class provide a customized class or record to match their specific requirements. If using AOT you must also ensure you capture the type in a <see cref="JsonSerializerContext"/>.
    /// </para>
    /// <para>See <see href="https://standard.site"/></para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "They're all various overloads for site types.")]
    public Document(
        Uri site,
        string title,
        DateTimeOffset publishedAt,
        string? path = null,
        string? description = null,
        Blob? coverImage = null,
        T? content = null,
        string? textContent = null,
        StrongReference? bskyPostRef = null,
        IEnumerable<string>? tags = null,
        DateTimeOffset? updatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(title);

        string convertedSite = site.ToString();
        convertedSite = convertedSite.EndsWith('/') ? convertedSite.Substring(0, convertedSite.Length - 1) : convertedSite;

        Site = convertedSite;
        Title = title;
        PublishedAt = publishedAt;

        Path = path;
        Description = description;
        CoverImage = coverImage;
        Content = content;
        TextContent = textContent;
        BSkyPostRef = bskyPostRef;

        if (tags is not null)
        {
            Tags = tags;
        }

        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a new <see cref="Document{T}"/> encapsulating what content contains.
    /// </summary>
    /// <param name="site">The publication <see cref="AtUri"/> for loose documents.</param>
    /// <param name="title">Title of the document.</param>
    /// <param name="publishedAt">Timestamp of the document publish time.</param>
    /// <param name="path">Optional path that is combined with site or publication url to construct a canonical URL to the document. Prepend with a leading slash.</param>
    /// <param name="description">Optional brief description or excerpt from the document.</param>
    /// <param name="coverImage">Optional image to used for thumbnail or cover image. Size must be less than 1MB.</param>
    /// <param name="content">Optional open union used to define the record's content. Each entry must specify a $type and may be extended with other lexicons to support additional content formats.</param>
    /// <param name="textContent">Plaintext representation of the documents contents. Should not contain markdown or other formatting.</param>
    /// <param name="bskyPostRef">Strong reference to a Bluesky post. Useful to keep track of comments off-platform.</param>
    /// <param name="tags">Array of strings used to tag or categorize the document. Avoid prepending tags with hashtags.</param>
    /// <param name="updatedAt">Timestamp of the documents last edit, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="site"/> or <paramref name="title"/> is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="content"/> is defined in the lexicon as an unconstrained union, meaning its type cannot be determined. It is expected that instances of this
    /// class provide a customized class or record to match their specific requirements. If using AOT you must also ensure you capture the type in a <see cref="JsonSerializerContext"/>.
    /// </para>
    /// <para>See <see href="https://standard.site"/></para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "They're all various overloads for site types.")]
    public Document(
        AtUri site,
        string title,
        DateTimeOffset publishedAt,
        string? path = null,
        string? description = null,
        Blob? coverImage = null,
        T? content = null,
        string? textContent = null,
        StrongReference? bskyPostRef = null,
        IEnumerable<string>? tags = null,
        DateTimeOffset? updatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(title);

        string convertedSite = site.ToString();
        convertedSite = convertedSite.EndsWith('/') ? convertedSite.Substring(0, convertedSite.Length - 1) : convertedSite;

        Site = convertedSite;
        Title = title;
        PublishedAt = publishedAt;

        Path = path;
        Description = description;
        CoverImage = coverImage;
        Content = content;
        TextContent = textContent;
        BSkyPostRef = bskyPostRef;

        if (tags is not null)
        {
            Tags = tags;
        }

        UpdatedAt = updatedAt;
    }


    /// <summary>
    /// Gets the canonical URL for the document, combining <see cref="Site"/> and path <see cref="Path"/>. 
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Lexicon definition has this property as either an https URI or an AtUri.")]
    [JsonIgnore]
    protected virtual string CanonicalUrl => $"{Site}{Path}";

    /// <summary>
    /// Points to a publication record (at://) or a publication url (https://) for loose documents. Avoid trailing slashes.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the site is not a valid URI or AtUri, or if it ends with a trailing slash.</exception>
    [JsonRequired]
    public string Site
    {
        get;

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? siteUri) &&
                siteUri is not null &&
                siteUri.Scheme != "https")
            {
                throw new ArgumentException("Site scheme must be https", nameof(Site));
            }

            if (siteUri is null &&
                !AtUri.TryParse(value , out _))
            {
                throw new ArgumentException("Site must be an Uri or AtUri", nameof(Site));
            }

            if (value.EndsWith('/'))
            {
                throw new ArgumentException("Site cannot end in a trailing slash", nameof(Site));
            }

            field = value;
        }
    }

    /// <summary>
    /// Optional path that is combined with site or publication url to construct a canonical URL to the document. Prepend with a leading slash.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the path does not start with a leading slash.</exception>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path
    {
        get;

        set
        {
            if (value is not null && !value.StartsWith('/'))
            {
                throw new ArgumentException("Path must start with a leading slash");
            }
            field = value;
        }
    }

    /// <summary>
    /// The title of the document.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the title is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the title exceeds the maximum length.</exception>
    [JsonRequired]
    public string Title
    {
        get;

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, 1280);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetGraphemeLength(), 128);

            field = value;
        }
    }

    /// <summary>
    /// Optional brief description or excerpt from the document.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the description exceeds the maximum length.</exception>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description {
        get;

        set
        {
            if (value is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, 3000);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetGraphemeLength(), 300);
            }

            field = value;
        }
    }

    /// <summary>
    /// Optional image to used for thumbnail or cover image. Size must be less than 1MB.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the MIME type is <see langword="null" /> or empty, or not an image MIME type.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the image size exceeds the maximum allowed size.</exception>  
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Blob? CoverImage
    {
        get;

        set
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrEmpty(value.MimeType);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Size, 1000000);

                if (!value.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("CoverImage does not have an image MIME type.", nameof(CoverImage));
                }
            }

            field = value;
        }
    }

    /// <summary>
    /// An optional open union used to define the record's content. Each entry must specify a $type and may be extended with other lexicons to support additional content formats.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Content { get; set; }

    /// <summary>
    /// The plaintext representation of the documents contents. Should not contain markdown or other formatting.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TextContent { get; set; }

    /// <summary>
    /// Strong reference to a Bluesky post. Useful to keep track of comments off-platform.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("bskyPostRef")]
    public StrongReference? BSkyPostRef { get; set; }

    /// <summary>
    /// Array of strings used to tag or categorize the document. Avoid prepending tags with hashtags.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? Tags { get; set; }

    /// <summary>
    /// Timestamp of the documents publish time.
    /// </summary>
    [JsonRequired]
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>
    /// Timestamp of the documents last edit, if any.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? UpdatedAt { get; set; }
}
