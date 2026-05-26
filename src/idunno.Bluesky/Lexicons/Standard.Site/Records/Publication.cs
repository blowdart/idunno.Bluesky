// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;
using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Standard.Site;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Record encapsulating where content lives.
/// </summary>
/// <remarks>
/// <para>See <see href="https://standard.site"/></para>
/// </remarks>
public record Publication : Publication<Preferences>
{
    /// <summary>
    /// Create a new <see cref="Publication"/> encapsulating where content lives.
    /// </summary>
    /// <param name="url">Base publication url (ex: https://standard.site). The canonical document URL is formed by combining this value with the document path.</param>
    /// <param name="name">Name of the publication. Required, must be &lt; 1280 characters and &lt; 128 graphemes.</param>
    /// <param name="icon">Optional square image to identify the publication. Should be at least 256x256 and &lt; 1Mb in size.</param>
    /// <param name="description">Optional brief description of the publication. If specified must be &lt; 3000 characters and &lt; 300 graphemes.</param>
    /// <param name="basicTheme">Optional simplified publication theme for tools and apps to utilize when displaying content.</param>
    /// <param name="preferences">Optional record containing platform specific preferences (with a few shared properties).</param>
    public Publication(Uri url, string name, Blob? icon = null, string? description = null, BasicTheme? basicTheme = null, Preferences? preferences = null)
        : base(url, name, icon, description, basicTheme, preferences)
    {
    }
}

/// <summary>
/// Record encapsulating where content lives.
/// </summary>
/// <typeparam name="T">Type of the preferences record.</typeparam>
/// <remarks>
/// <para>See <see href="https://standard.site"/></para>
/// </remarks>
public record Publication<T> : AtProtoRecord where T : Preferences
{
    /// <summary>
    /// Create a new <see cref="Publication{T}"/> encapsulating where content lives.
    /// </summary>
    /// <param name="url">Base publication url (ex: https://standard.site). The canonical document URL is formed by combining this value with the document path.</param>
    /// <param name="name">Name of the publication. Required, must be &lt; 1280 characters and &lt; 128 graphemes.</param>
    /// <param name="icon">Optional square image to identify the publication. Should be at least 256x256 and &lt; 1Mb in size.</param>
    /// <param name="description">Optional brief description of the publication. If specified must be &lt; 3000 characters and &lt; 300 graphemes.</param>
    /// <param name="basicTheme">Optional simplified publication theme for tools and apps to utilize when displaying content.</param>
    /// <param name="preferences">Optional record containing platform specific preferences (with a few shared properties).</param>
    /// <exception cref="ArgumentException">Thrown when the provided arguments are invalid.</exception>
    public Publication(Uri url, string name, Blob? icon = null, string? description = null, BasicTheme? basicTheme = null, T? preferences = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrEmpty(name);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, 1280);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.GetGraphemeLength(), 128);

        if (icon is not null)
        {
            if (!icon.MimeType.StartsWith("image/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Icon mime type must be an image mime type.", nameof(icon));
            }

            ArgumentOutOfRangeException.ThrowIfGreaterThan(icon.Size, 1000000);
        }

        if (description is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(description.Length, 3000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(description.GetGraphemeLength(), 300);
        }

        Url = url;
        Name = name;
        Icon = icon;
        Description = description;
        BasicTheme = basicTheme;
        Preferences = preferences;
    }

    /// <summary>
    /// Gets and sets the lexicon type identifier for the record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Customize this property in derived classes to provide a specific lexicon type identifier.
    /// This is used when serializing the record to include the $type property required by your lexicon.
    /// </para>
    /// </remarks>
    [JsonInclude]
    [JsonPropertyName("$type")]
    public virtual string? Type { get; set; } = "site.standard.publication";

    /// <summary>
    /// Gets the base publication url (ex: https://standard.site). The canonical document URL is formed by combining this value with the document path.
    /// </summary>
    [JsonRequired]
    public Uri Url { get; set; }

    /// <summary>
    /// Gets the name of the publication.
    /// </summary>
    [JsonRequired]
    public string Name { get; set; }

    /// <summary>
    /// Gets the optional image to identify the publication, if any.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Blob? Icon { get; set; }

    /// <summary>
    /// Gets the description of the publication, if any.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets the simplified publication theme for tools and apps to utilize when displaying content, if any.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BasicTheme? BasicTheme { get; set; }

    /// <summary>
    /// Record containing platform specific preferences (with a few shared properties).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Preferences { get; set; }
}
