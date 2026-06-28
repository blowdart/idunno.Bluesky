// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Base class for all AtProto errors.
/// </summary>
public abstract class AtProtoError : AtErrorDetail
{
    /// <summary>
    /// Creates a new instance of the <see cref="AtProtoError"/> from an instance of <see cref="AtErrorDetail"/>.
    /// </summary>
    /// <param name="other">The <see cref="AtErrorDetail"/> instance to copy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    protected AtProtoError(AtErrorDetail other) : base(other)
    {
        ArgumentNullException.ThrowIfNull(other);
    }

    /// <summary>
    /// Attempts to map the specified <see cref="AtErrorDetail"/> to a specific <see cref="AtProtoError"/> subclass.
    /// </summary>
    /// <param name="atErrorDetail">The <see cref="AtErrorDetail"/> instance to map.</param>
    /// <returns>A specific <see cref="AtProtoError"/> subclass if a match is found; otherwise, the original <see cref="AtErrorDetail"/>.</returns>
    public static AtErrorDetail? Map(AtErrorDetail? atErrorDetail)
    {
        if (atErrorDetail is null)
        {
            return null;
        }

        return atErrorDetail.Error switch
        {
            AccountNotFound.ErrorTitle => new AccountNotFound(atErrorDetail),
            AccountTakedown.ErrorTitle => new AccountTakedown(atErrorDetail),
            AuthenticationRequired.ErrorTitle => new AuthenticationRequired(atErrorDetail),
            AuthFactorTokenRequired.ErrorTitle => new AuthFactorTokenRequired(atErrorDetail),
            BadExpiration.ErrorTitle => new BadExpiration(atErrorDetail),
            BlobNotFound.ErrorTitle => new BlobNotFound(atErrorDetail),
            BlockNotFound.ErrorTitle => new BlockNotFound(atErrorDetail),
            ConsumerTooSlow.ErrorTitle => new ConsumerTooSlow(atErrorDetail),
            DidDeactivated.ErrorTitle => new DidDeactivated(atErrorDetail),
            DidNotFound.ErrorTitle => new DidNotFound(atErrorDetail),
            DuplicateCreate.ErrorTitle => new DuplicateCreate(atErrorDetail),
            ExpiredToken.ErrorTitle => new ExpiredToken(atErrorDetail),
            FutureCursor.ErrorTitle => new FutureCursor(atErrorDetail),
            HandleNotFound.ErrorTitle => new HandleNotFound(atErrorDetail),
            HandleNotAvailable.ErrorTitle => new HandleNotAvailable(atErrorDetail),
            HeadNotFound.ErrorTitle => new HeadNotFound(atErrorDetail),
            HostBanned.ErrorTitle => new HostBanned(atErrorDetail),
            HostNotFound.ErrorTitle => new HostNotFound(atErrorDetail),
            IncompatibleDidDoc.ErrorTitle => new IncompatibleDidDoc(atErrorDetail),
            InvalidEmail.ErrorTitle => new InvalidEmail(atErrorDetail),
            InvalidInviteCode.ErrorTitle => new InvalidInviteCode(atErrorDetail),
            InvalidPasscode.ErrorTitle => new InvalidPasscode(atErrorDetail),
            InvalidRequest.ErrorTitle => new InvalidRequest(atErrorDetail),
            InvalidSwap.ErrorTitle => new InvalidSwap(atErrorDetail),
            InvalidToken.ErrorTitle => new InvalidToken(atErrorDetail),
            MethodNotImplemented.ErrorTitle => new MethodNotImplemented(atErrorDetail),
            RecordNotFound.ErrorTitle => new RecordNotFound(atErrorDetail),
            RepoDeactivated.ErrorTitle => new RepoDeactivated(atErrorDetail),
            RepoNotFound.ErrorTitle => new RepoNotFound(atErrorDetail),
            RepoTakenDown.ErrorTitle => new RepoTakenDown(atErrorDetail),
            RepoSuspended.ErrorTitle => new RepoSuspended(atErrorDetail),
            TokenRequired.ErrorTitle => new TokenRequired(atErrorDetail),
            UnresolvableDid.ErrorTitle => new UnresolvableDid(atErrorDetail),
            UnsupportedDomain.ErrorTitle => new UnsupportedDomain(atErrorDetail),
            _ => atErrorDetail
        };
    }
}