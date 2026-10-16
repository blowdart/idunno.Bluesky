// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.AtProto;
using idunno.Bluesky.Contact;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Imports contacts and returns matching profiles with their original contact indexes.
    /// </summary>
    /// <param name="token">The single-use token returned by phone verification.</param>
    /// <param name="contacts">The contact phone numbers in E.164 format, in their original order.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task representing the authenticated contact request.</returns>
    /// <exception cref="ArgumentNullException">An argument documented as required is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The contact count is less than 1 or greater than 1000, or the maximum response size is not positive.</exception>
    /// <exception cref="ArgumentException">The contacts contain a <see langword="null"/> entry.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>Requires authentication. Named errors: <c>InvalidDid</c>, <c>InvalidContacts</c>, <c>TooManyContacts</c>, <c>InvalidToken</c>, <c>InternalError</c>.</para>
    /// <para>Phone numbers, verification codes and import tokens are sensitive. Do not log or persist request or response bodies.</para>
    /// <para>The token is valid for one import. The <paramref name="contacts"/> should exclude the verified phone number. The service discards phone numbers it cannot normalize.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<ReadOnlyCollection<ContactMatch>>> ImportContacts(
        string token,
        ICollection<string> contacts,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.ImportContacts(
            token: token,
            contacts: contacts,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
