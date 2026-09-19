using idunno.AtProto;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Sets the activity notification settings for the specified <paramref name="subject"/>. Requires authentication.
    /// </summary>
    /// <param name="subject">The <see cref="AtProto.Did"/> of the actor for whom activity notifications should be set.</param>
    /// <param name="posts">Flag indicating whether notifications should be enabled for posts.</param>
    /// <param name="replies">Flag indicating whether notifications should be enabled for replies. <paramref name="posts"/> must also be <see langword="true"/> for this setting to work.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="subject"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="replies"/> is <see langword="true"/> but <paramref name="posts"/> is <see langword="false"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
    public async Task<AtProtoHttpResult<SubjectActivitySubscription>> SetActivitySubscription(
    Did subject,
    bool posts,
    bool replies,
    CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (replies && !posts)
        {
            throw new ArgumentException("cannot be true if posts is false", nameof(replies));
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.PutActivitySubscription(
            new SubjectActivitySubscription(subject, new ActivitySubscription(posts, replies)),
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}