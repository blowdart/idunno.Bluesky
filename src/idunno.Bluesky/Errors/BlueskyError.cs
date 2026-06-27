// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;

#pragma warning disable IDE0130
namespace idunno.Bluesky;
#pragma warning restore IDE0130

/// <summary>
/// Base class for all Bluesky errors.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Base class for all Bluesky errors")]
public abstract class BlueskyError : AtErrorDetail
{
    /// <summary>
    /// Creates a new instance of the <see cref="BlueskyError"/> from an instance of <see cref="AtErrorDetail"/>.
    /// </summary>
    /// <param name="other">The <see cref="AtErrorDetail"/> instance to copy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    protected BlueskyError(AtErrorDetail other) : base(other)
    {
        ArgumentNullException.ThrowIfNull(other);
    }

    /// <summary>
    /// Maps the <see cref="AtErrorDetail"/> to a <see cref="BlueskyError"/>, if possible.
    /// </summary>
    /// <param name="atErrorDetail">The <see cref="AtErrorDetail"/> instance to map.</param>
    /// <returns>A <see cref="BlueskyError"/> instance if mapping is possible; otherwise, <see langword="null"/>.</returns>
    public static AtErrorDetail? Map(AtErrorDetail? atErrorDetail)
    {
        if (atErrorDetail is null)
        {
            return null;
        }

        return atErrorDetail.Error switch
        {
            AccountSuspended.ErrorTitle => new AccountSuspended(atErrorDetail),
            BlockedActor.ErrorTitle => new BlockedActor(atErrorDetail),
            BlockedSubject.ErrorTitle => new BlockedSubject(atErrorDetail),
            ConversationLocked.ErrorTitle => new ConversationLocked(atErrorDetail),
            EnabledJoinLinkAlreadyExists.ErrorTitle => new EnabledJoinLinkAlreadyExists(atErrorDetail),
            FollowRequired.ErrorTitle => new FollowRequired(atErrorDetail),
            InsufficientRole.ErrorTitle => new InsufficientRole(atErrorDetail),
            InvalidCode.ErrorTitle => new InvalidCode(atErrorDetail),
            InvalidConversation.ErrorTitle => new InvalidConversation(atErrorDetail),
            LinkDisabled.ErrorTitle => new LinkDisabled(atErrorDetail),
            MemberLimitReached.ErrorTitle => new MemberLimitReached(atErrorDetail),
            MessageDeleteNotAllowed.ErrorTitle => new MessageDeleteNotAllowed(atErrorDetail),
            MessagesDisabled.ErrorTitle => new MessagesDisabled(atErrorDetail),
            NewAccountCannotCreateGroup.ErrorTitle => new NewAccountCannotCreateGroup(atErrorDetail),
            NoJoinLink.ErrorTitle => new NoJoinLink(atErrorDetail),
            NotFollowedBySender.ErrorTitle => new NotFollowedBySender(atErrorDetail),
            ReactionInvalidValue.ErrorTitle => new ReactionInvalidValue(atErrorDetail),
            ReactionLimitReached.ErrorTitle => new ReactionLimitReached(atErrorDetail),
            ReactionMessageDeleted.ErrorTitle => new ReactionMessageDeleted(atErrorDetail),
            ReactionNotAllowed.ErrorTitle => new ReactionNotAllowed(atErrorDetail),
            RecipientNotFound.ErrorTitle => new RecipientNotFound(atErrorDetail),
            UserForbidsGroups.ErrorTitle => new UserForbidsGroups(atErrorDetail),
            UserKicked.ErrorTitle => new UserKicked(atErrorDetail),
            _ => atErrorDetail
        };
    }
}
