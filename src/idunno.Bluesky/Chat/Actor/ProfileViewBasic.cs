// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Chat.Actor;

/// <summary>
/// A basic view of a profile, used in the context of chat conversations.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record ProfileViewBasic : View
{
    [JsonConstructor]
    internal ProfileViewBasic(
        Did did,
        Handle handle,
        Uri? avatar,
        string? displayName,
        ProfileAssociated? associated,
        ActorViewerState? viewer,
        IReadOnlyCollection<Label>? labels,
        DateTimeOffset? createdAt,
        VerificationState? verification,
        MemberKind? kind)
     : base()
    {
        Did = did;
        Handle = handle;
        Avatar = avatar;
        DisplayName = displayName;
        Associated = associated;
        Viewer = viewer;
        Labels = labels ?? [];
        CreatedAt = createdAt;
        Verification = verification;
        Kind = kind;
    }

    /// <summary>
    /// Gets the did of the actor.
    /// </summary>
    [JsonRequired]
    public Did Did { get; init; }

    /// <summary>
    /// Gets the handle of the actor.
    /// </summary>
    [JsonRequired]
    public Handle Handle { get; init; }

    /// <summary>
    /// Gets the display name, if any, of the actor.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the avatar of the actor.
    /// </summary>
    public Uri? Avatar { get; init; }

    /// <summary>
    /// Gets the <see cref="ProfileAssociated">Associated properties</see> for the subject.
    /// </summary>
    public ProfileAssociated? Associated { get; init; }

    /// <summary>
    /// Gets metadata about the requesting account's relationship with the subject account. Only has meaningful content for authenticated requests.
    /// </summary>
    public ActorViewerState? Viewer { get; init; }

    /// <summary>
    /// Gets any labels applied to the actor.
    /// </summary>
    /// <remarks>
    ///<para>
    /// Labels will only be populated if a list of subscribed labelers from a user's <see cref="Preferences"/>
    /// is passed into an API which returns this class.
    ///</para>
    /// </remarks>
    public IReadOnlyCollection<Label> Labels { get; init; }

    /// <summary>
    /// Gets the date and time the actor was created at, if any
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Flag indicating the actor cannot actively participate in conversations
    /// </summary>
    public bool? ChatDisabled { get; init; }
    
    /// <summary>
    /// Gets the verification state of the actor.
    /// </summary>
    public VerificationState? Verification { get; init; }

    /// <summary>
    /// Gets the kind of member this actor is in the context of a conversation.
    /// </summary>
    public MemberKind? Kind { get; init; }

    /// <summary>
    /// Returns a list of label values for labels that appear to have been applied by the actor to themselves, based on the label source and uri.
    /// </summary>
    /// <remarks>
    /// <para>Known Bluesky label values can be found in <see cref="SelfLabelValues"/>.</para>
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyList<string> SelfLabels
    {
        get
        {
            field ??= Labels
                .Where(l => (l.Source == Did &&
                             l.Uri == $"at://{Did}/app.bsky.actor.profile/self"))
                .Select(v => v.Value)
                .Distinct().ToList().AsReadOnly();

            return field;
        }

        private set;
    }

    /// <summary>
    /// Gets a string representation of the <see cref="ProfileView"/>.
    /// This returns the actor's display name, if any, otherwise returns the actor's handle.
    /// </summary>
    /// <returns>A string representation of the <see cref="ProfileView"/>.</returns>
    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            return $"{DisplayName} ({Handle})";
        }
        else
        {
            return $"{Handle}";
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get
        {
            return $"{{{Handle} ({Did})}}";
        }
    }

}
