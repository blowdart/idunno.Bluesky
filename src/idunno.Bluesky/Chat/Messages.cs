// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.Bluesky.Chat;

/// <summary>
/// Implements a paged collection of messages from a direct message conversation.
/// </summary>
public class Messages : PagedViewReadOnlyCollection<MessageViewBase>
{
    /// <summary>
    /// Creates a new instance of <see cref="Messages"/> with an empty list and no cursor.
    /// </summary>
    public Messages() : this([], null, null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Messages"/>.
    /// </summary>
    /// <param name="list">The list of <see cref="MessageViewBase"/> to create this instance of <see cref="Messages"/> from.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="relatedProfiles">An optional collection of the profiles of every member who authored or reacted to the messages in <paramref name="list"/>, including those referred to by system messages.</param>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Pre-existing overload shape, extended with an optional parameter to avoid a breaking change.")]
    public Messages(IList<MessageViewBase> list, string? cursor = null, IReadOnlyCollection<Actor.ProfileViewBasic>? relatedProfiles = null) : base(list, cursor)
    {
        RelatedProfiles = relatedProfiles is null ? [] : new List<Actor.ProfileViewBasic>(relatedProfiles).AsReadOnly();
    }

    /// <summary>
    /// Creates a new instance of <see cref="Messages"/>.
    /// </summary>
    /// <param name="collection">A collection of <see cref="MessageViewBase"/> to create this instance of <see cref="Messages"/> from.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="relatedProfiles">An optional collection of the profiles of every member who authored or reacted to the messages in <paramref name="collection"/>, including those referred to by system messages.</param>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Pre-existing overload shape, extended with an optional parameter to avoid a breaking change.")]
    public Messages(ICollection<MessageViewBase> collection, string? cursor = null, IReadOnlyCollection<Actor.ProfileViewBasic>? relatedProfiles = null) : this([.. collection], cursor, relatedProfiles)
    {
    }

    /// <summary>
    /// Gets the profiles of every member who authored or reacted to the messages in this collection,
    /// including the members referred to by any system messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="SystemMessageView"/> refers to the users it concerns by <see cref="AtProto.Did"/> only, so this collection is
    /// the only source of those users' profiles. It is empty if the service did not return any related profiles.
    /// </para>
    /// <para>
    /// The value supplied is copied, so later changes to the collection assigned are not reflected here.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<Actor.ProfileViewBasic> RelatedProfiles { get; }
}
