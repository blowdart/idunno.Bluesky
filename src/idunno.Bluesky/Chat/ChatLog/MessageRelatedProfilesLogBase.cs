// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Base record for chat log message entries, which also includes the profiles referred to in the message.
/// </summary>
public abstract record MessageRelatedProfilesLogBase : MessageLogBase
{
    /// <summary>
    /// Creates a new instance of <see cref="LogBase"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    /// <param name="message">A <see cref="MessageViewBase">view</see> over the message the log entry refers to.</param>
    /// <param name="relatedProfiles"></param>
    private protected MessageRelatedProfilesLogBase(string conversationId, string revision, MessageViewBase message, IReadOnlyCollection<ProfileViewBasic> relatedProfiles)
        : base(conversationId, revision, message)
    {
        RelatedProfiles = relatedProfiles;
    }

    /// <summary>
    /// Gets Profiles referred to in the system message.
    /// </summary>
    /// <exception cref="ArgumentNullException">The collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The collection contains a <see langword="null"/> entry.</exception>
    [JsonRequired]
    public IReadOnlyCollection<ProfileViewBasic> RelatedProfiles
    {
        get => _relatedProfiles;
        init => _relatedProfiles = ChatCollection.Copy(value);
    }

    private readonly IReadOnlyCollection<ProfileViewBasic> _relatedProfiles = null!;
}