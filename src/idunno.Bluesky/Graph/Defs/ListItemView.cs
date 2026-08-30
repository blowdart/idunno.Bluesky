// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Actor;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Graph;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a view over an individual item in a list.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record ListItemView : View
{
    /// <summary>
    /// Creates a new instance of <see cref="ListItemView"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of list item.</param>
    /// <param name="subject">A <see cref="ProfileView"/> of the actor the list item refers to.</param>
    /// <param name="subjectOptedOut">A flag indicating the subject has opted out of appearing in the reference list.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="subject"/> are <see langword="null"/>.</exception>
    internal ListItemView(AtUri uri, ProfileView subject, bool? subjectOptedOut)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(subject);

        Uri = uri;
        Subject = subject;
        SubjectOptedOut = subjectOptedOut;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the list item.
    /// </summary>
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Gets a <see cref="ProfileView"/> of the actor the list item refers to.
    /// </summary>
    [JsonRequired]
    public ProfileView Subject { get; init; }

    /// <summary>
    /// Gets a flag indidicated the subject has opted out of appearing in the reference list. Only set when the viewer owns the list.
    /// </summary>
    public bool? SubjectOptedOut { get; init; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get
        {
            return $"{{{Subject.Handle} => {Uri}}}";
        }
    }
}