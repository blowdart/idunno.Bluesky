// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Collections.ObjectModel;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Contact;
using idunno.Bluesky.Contact.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Imports contacts and returns matching profiles with their original contact indexes.
    /// </summary>
    /// <param name="token">The single-use token returned by phone verification.</param>
    /// <param name="contacts">The contact phone numbers in E.164 format, in their original order.</param>
    /// <param name="service">The personal data server to proxy the request through.</param>
    /// <param name="accessCredentials">The credentials used to authenticate the request.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="onCredentialsUpdated">An optional callback to persist updated credentials.</param>
    /// <param name="loggerFactory">An optional factory for request logging.</param>
    /// <param name="maximumResponseSize">The maximum response body size in bytes.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task representing the authenticated contact request.</returns>
    /// <exception cref="ArgumentNullException">An argument documented as required is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The contact count is less than 1 or greater than 1000, or the maximum response size is not positive.</exception>
    /// <exception cref="ArgumentException">The contacts contain a <see langword="null"/> entry.</exception>
    /// <remarks>
    /// <para>Requires authentication. Named errors: <c>InvalidDid</c>, <c>InvalidContacts</c>, <c>TooManyContacts</c>, <c>InvalidToken</c>, <c>InternalError</c>.</para>
    /// <para>Phone numbers, verification codes and import tokens are sensitive. Do not log or persist request or response bodies.</para>
    /// <para>The token is valid for one import. The <paramref name="contacts"/> should exclude the verified phone number. The service discards phone numbers it cannot normalize.</para>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Contact request and response types are source-generated; ContactDiscoveryTests exercises this endpoint with reflection disabled.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Contact request and response types are source-generated; ContactDiscoveryTests exercises this endpoint with reflection disabled.")]
    public static async Task<AtProtoHttpResult<ReadOnlyCollection<ContactMatch>>> ImportContacts(
        string token,
        ICollection<string> contacts,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentOutOfRangeException.ThrowIfLessThan(contacts.Count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(contacts.Count, 1000);
        List<string> contactSnapshot = [.. contacts];
        if (contactSnapshot.Any(contact => contact is null))
        {
            throw new ArgumentException("Contacts cannot contain null entries.", nameof(contacts));
        }

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseSize);

        BlueskyHttpClient<ImportContactsResponse> client = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
        AtProtoHttpResult<ImportContactsResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/app.bsky.contact.importContacts",
            record: new ImportContactsRequest(token, contactSnapshot),
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AtProtoHttpResult<ReadOnlyCollection<ContactMatch>>(
            response.Succeeded ? WithoutNullEntries(response.Result.MatchesAndContactIndexes, service, nameof(response.Result.MatchesAndContactIndexes), loggerFactory).AsReadOnly() : null,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }
}
