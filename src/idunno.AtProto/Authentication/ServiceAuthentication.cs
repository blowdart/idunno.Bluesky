// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Repo;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

#pragma warning disable IDE0130 // Authentication features share the protocol's root namespace.
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Verifies short-lived AT Protocol service authentication tokens.
/// </summary>
public static class ServiceAuthentication
{
    private const int MaximumTokenLength = 8192;
    private static readonly TimeSpan s_defaultMaximumLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Verifies a service token's signature, issuer, audience, method and lifetime.
    /// </summary>
    /// <param name="token">The compact signed JWT.</param>
    /// <param name="audience">The exact destination service reference.</param>
    /// <param name="method">The exact XRPC method receiving the token.</param>
    /// <returns>The verified identity and identifiers for replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The HTTP request times out.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Requires an integer lifetime of at most 15 minutes, validates an optional integer <c>nbf</c> timestamp,
    /// allows five seconds of clock skew, and accepts only ES256 or ES256K signatures made by the issuer's matching
    /// <c>#atproto</c> key.</para>
    /// <para>This method does not prevent replay. The caller must atomically reserve the issuer and token identifier
    /// and the token hash before accepting a request, retaining them through the expiry plus clock skew.</para>
    /// </remarks>
    public static Task<ServiceAuthenticationResult> VerifyAsync(
        string token,
        AtProtoServiceReference audience,
        Nsid method)
    {
        return VerifyAsync(
            token: token,
            audience: audience,
            method: method,
            cancellationToken: default);
    }

    /// <summary>
    /// Verifies a service token's signature, issuer, audience, method and lifetime.
    /// </summary>
    /// <param name="token">The compact signed JWT.</param>
    /// <param name="audience">The exact destination service reference.</param>
    /// <param name="method">The exact XRPC method receiving the token.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The verified identity and identifiers for replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled or the HTTP request times out.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Requires an integer lifetime of at most 15 minutes, validates an optional integer <c>nbf</c> timestamp,
    /// allows five seconds of clock skew, and accepts only ES256 or ES256K signatures made by the issuer's matching
    /// <c>#atproto</c> key.</para>
    /// <para>This method does not prevent replay. The caller must atomically reserve the issuer and token identifier
    /// and the token hash before accepting a request, retaining them through the expiry plus clock skew.</para>
    /// </remarks>
    public static Task<ServiceAuthenticationResult> VerifyAsync(
        string token,
        AtProtoServiceReference audience,
        Nsid method,
        CancellationToken cancellationToken)
    {
        return VerifyAsync(
            token: token,
            audience: audience,
            method: method,
            timeProvider: null,
            maximumLifetime: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Verifies a service token's signature, issuer, audience, method and lifetime.
    /// </summary>
    /// <param name="token">The compact signed JWT.</param>
    /// <param name="audience">The exact destination service reference.</param>
    /// <param name="method">The exact XRPC method receiving the token.</param>
    /// <param name="timeProvider">The clock used to validate the lifetime, or <see langword="null"/> to use the system clock.</param>
    /// <param name="maximumLifetime">The maximum accepted token lifetime, or <see langword="null"/> to use the default of 15 minutes.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The verified identity and identifiers for replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Requires an integer lifetime no greater than <paramref name="maximumLifetime"/>, defaults to 15 minutes,
    /// validates an optional integer <c>nbf</c> timestamp, allows five seconds of clock skew, and accepts only
    /// ES256 or ES256K signatures made by the issuer's matching <c>#atproto</c> key.</para>
    /// <para>This method does not prevent replay. The caller must atomically reserve the issuer and token identifier
    /// and the token hash before accepting a request, retaining them through the expiry plus clock skew.</para>
    /// </remarks>
    public static async Task<ServiceAuthenticationResult> VerifyAsync(
        string token,
        AtProtoServiceReference audience,
        Nsid method,
        TimeProvider? timeProvider,
        TimeSpan? maximumLifetime,
        CancellationToken cancellationToken)
    {
        using HttpClientLease lease = new(httpClient: null, timeout: null);
        return await VerifyAsync(
            token: token,
            audience: audience,
            method: method,
            httpClient: lease.Client,
            timeProvider: timeProvider,
            maximumLifetime: maximumLifetime,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies a service token's signature, issuer, audience, method and lifetime.
    /// </summary>
    /// <param name="token">The compact signed JWT.</param>
    /// <param name="audience">The exact destination service reference.</param>
    /// <param name="method">The exact XRPC method receiving the token.</param>
    /// <param name="httpClient">The HTTP client used to resolve the issuer's DID document.</param>
    /// <param name="timeProvider">An optional clock used to validate the lifetime.</param>
    /// <param name="maximumLifetime">The maximum accepted token lifetime. The default is 15 minutes.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The verified identity and identifiers for replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>Requires an integer lifetime no greater than <paramref name="maximumLifetime"/>, defaults to 15 minutes,
    /// validates an optional integer <c>nbf</c> timestamp, allows five seconds of clock skew, and accepts only
    /// ES256 or ES256K signatures made by the issuer's matching <c>#atproto</c> key.</para>
    /// <para>This method does not prevent replay. The caller must atomically reserve the issuer and token identifier
    /// and the token hash before accepting a request, retaining them through the expiry plus clock skew.</para>
    /// <para>This method uses <paramref name="httpClient"/> as supplied, adds no SSRF protection and does not dispose
    /// it. An attacker can choose a <c>did:web</c> issuer whose hostname resolves to loopback, link-local or private
    /// addresses, including cloud metadata endpoints. The handler must check resolved IP addresses at connection time
    /// and reject those destinations; checking a hostname before connecting does not prevent DNS rebinding. Disable
    /// automatic redirects, which can send a request to an unchecked destination.</para>
    /// <para>Register an <see cref="IHttpClientFactory"/> with
    /// <see cref="ServiceCollectionExtensions.AddAtProtoHttpClient(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
    /// and call <c>factory.CreateClient("idunno.atproto")</c> for the library's protected client, or provide equivalent
    /// protections in a custom handler. Calling <c>factory.CreateClient()</c> does not select the named client.</para>
    /// </remarks>
    public static async Task<ServiceAuthenticationResult> VerifyAsync(
        string token,
        AtProtoServiceReference audience,
        Nsid method,
        HttpClient httpClient,
        TimeProvider? timeProvider = null,
        TimeSpan? maximumLifetime = null,
        CancellationToken cancellationToken = default)
        => await VerifyCoreAsync(
            token: token,
            audience: audience,
            method: method,
            httpClient: httpClient,
            timeProvider: timeProvider,
            maximumLifetime: maximumLifetime,
            allowLegacyModerationAudience: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Verifies an independent labeler's report token without granting team administration authority.
    /// </summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <returns>The authenticated reporter and replay identifiers, not an administrative caller.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The HTTP request times out.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Accepts only <c>com.atproto.moderation.createReport</c>, addressed either to the exact
    /// <c>labeler#atproto_labeler</c> service or to the same bare labeler DID for legacy PDS proxies.
    /// This intake verifier grants no administrative authority, and
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/> remains exact-audience.
    /// Ozone administration uses the separate <see cref="VerifyOzoneAdministrationAsync(string, Did, Nsid)"/> boundary.</para>
    /// <para>Uses the same signature, key, lifetime and replay-identifier validation as
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/>. The caller must durably prevent replay before
    /// accepting the report. A verified reporter does not need labeler team membership.</para>
    /// </remarks>
    public static Task<ModerationReportAuthenticationResult> VerifyModerationReportAsync(
        string token,
        Did labeler)
    {
        return VerifyModerationReportAsync(
            token: token,
            labeler: labeler,
            cancellationToken: default);
    }

    /// <summary>
    /// Verifies an independent labeler's report token without granting team administration authority.
    /// </summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The authenticated reporter and replay identifiers, not an administrative caller.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled or the HTTP request times out.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Accepts only <c>com.atproto.moderation.createReport</c>, addressed either to the exact
    /// <c>labeler#atproto_labeler</c> service or to the same bare labeler DID for legacy PDS proxies.
    /// This intake verifier grants no administrative authority, and
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/> remains exact-audience.
    /// Ozone administration uses the separate <see cref="VerifyOzoneAdministrationAsync(string, Did, Nsid)"/> boundary.</para>
    /// <para>Uses the same signature, key, lifetime and replay-identifier validation as
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/>. The caller must durably prevent replay before
    /// accepting the report. A verified reporter does not need labeler team membership.</para>
    /// </remarks>
    public static Task<ModerationReportAuthenticationResult> VerifyModerationReportAsync(
        string token,
        Did labeler,
        CancellationToken cancellationToken)
    {
        return VerifyModerationReportAsync(
            token: token,
            labeler: labeler,
            timeProvider: null,
            maximumLifetime: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Verifies an independent labeler's report token without granting team administration authority.
    /// </summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="timeProvider">The clock used to validate the lifetime, or <see langword="null"/> to use the system clock.</param>
    /// <param name="maximumLifetime">The maximum accepted token lifetime, or <see langword="null"/> to use the default of 15 minutes.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The authenticated reporter and replay identifiers, not an administrative caller.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Accepts only <c>com.atproto.moderation.createReport</c>, addressed either to the exact
    /// <c>labeler#atproto_labeler</c> service or to the same bare labeler DID for legacy PDS proxies.
    /// This intake verifier grants no administrative authority, and
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>
    /// remains exact-audience. Ozone administration uses the separate
    /// <see cref="VerifyOzoneAdministrationAsync(string, Did, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/> boundary.</para>
    /// <para>Uses the same signature, key, lifetime and required replay-identifier validation as
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>.
    /// The caller must durably prevent replay before accepting the report. A verified reporter does not need labeler
    /// team membership.</para>
    /// </remarks>
    public static async Task<ModerationReportAuthenticationResult> VerifyModerationReportAsync(
        string token,
        Did labeler,
        TimeProvider? timeProvider,
        TimeSpan? maximumLifetime,
        CancellationToken cancellationToken)
    {
        using HttpClientLease lease = new(httpClient: null, timeout: null);
        return await VerifyModerationReportAsync(
            token: token,
            labeler: labeler,
            httpClient: lease.Client,
            timeProvider: timeProvider,
            maximumLifetime: maximumLifetime,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies an independent labeler's report token without granting team administration authority.
    /// </summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="httpClient">The HTTP client used to resolve the reporter's DID document.</param>
    /// <param name="timeProvider">An optional clock used to validate the lifetime.</param>
    /// <param name="maximumLifetime">The maximum accepted token lifetime. The default is 15 minutes.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The authenticated reporter and replay identifiers, not an administrative caller.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>Accepts only <c>com.atproto.moderation.createReport</c>, addressed either to the exact
    /// <c>labeler#atproto_labeler</c> service or to the same bare labeler DID for legacy PDS proxies.
    /// This intake verifier grants no administrative authority, and
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>
    /// remains exact-audience. Ozone administration uses the separate
    /// <see cref="VerifyOzoneAdministrationAsync(string, Did, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/> boundary.</para>
    /// <para>Uses the same signature, key, lifetime and required replay-identifier validation as
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>.
    /// The caller must durably prevent replay before accepting the report. A verified reporter does not need labeler
    /// team membership.</para>
    /// <para>This method uses <paramref name="httpClient"/> as supplied, adds no SSRF protection and does not dispose
    /// it. An attacker can choose a <c>did:web</c> issuer whose hostname resolves to loopback, link-local or private
    /// addresses, including cloud metadata endpoints. The handler must check resolved IP addresses at connection time
    /// and reject those destinations; checking a hostname before connecting does not prevent DNS rebinding. Disable
    /// automatic redirects, which can send a request to an unchecked destination.</para>
    /// <para>Register an <see cref="IHttpClientFactory"/> with
    /// <see cref="ServiceCollectionExtensions.AddAtProtoHttpClient(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
    /// and call <c>factory.CreateClient("idunno.atproto")</c> for the library's protected client, or provide equivalent
    /// protections in a custom handler. Calling <c>factory.CreateClient()</c> does not select the named client.</para>
    /// </remarks>
    public static async Task<ModerationReportAuthenticationResult> VerifyModerationReportAsync(
        string token,
        Did labeler,
        HttpClient httpClient,
        TimeProvider? timeProvider = null,
        TimeSpan? maximumLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(labeler);
        ServiceAuthenticationResult verified = await VerifyCoreAsync(
            token: token,
            audience: new(labeler, "atproto_labeler"),
            method: new("com.atproto.moderation.createReport"),
            httpClient: httpClient,
            timeProvider: timeProvider,
            maximumLifetime: maximumLifetime,
            allowLegacyModerationAudience: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new(verified);
    }

    /// <summary>
    /// Verifies an authenticated Ozone appeal submission addressed to an independent labeler.
    /// </summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <returns>The authenticated reporter and replay identifiers, not an administrative caller.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The HTTP request times out.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Requires the <c>tools.ozone.inbox.appealActionedSubject</c> method claim and accepts either the exact
    /// <c>labeler#atproto_labeler</c> audience or the same bare labeler DID used by PDS proxies.
    /// This intake verifier grants no administrative authority, and
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/> remains exact-audience.
    /// Ozone administration uses the separate <see cref="VerifyOzoneAdministrationAsync(string, Did, Nsid)"/> boundary.</para>
    /// <para>Uses the same signature, key, lifetime and replay-identifier validation as
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/>. The caller must durably prevent replay before
    /// accepting the appeal.</para>
    /// </remarks>
    public static Task<ModerationReportAuthenticationResult> VerifyModerationAppealAsync(
        string token,
        Did labeler)
    {
        return VerifyModerationAppealAsync(
            token: token,
            labeler: labeler,
            cancellationToken: default);
    }

    /// <summary>
    /// Verifies an authenticated Ozone appeal submission addressed to an independent labeler.
    /// </summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The authenticated reporter and replay identifiers, not an administrative caller.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled or the HTTP request times out.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Requires the <c>tools.ozone.inbox.appealActionedSubject</c> method claim and accepts either the exact
    /// <c>labeler#atproto_labeler</c> audience or the same bare labeler DID used by PDS proxies.
    /// This intake verifier grants no administrative authority, and
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/> remains exact-audience.
    /// Ozone administration uses the separate <see cref="VerifyOzoneAdministrationAsync(string, Did, Nsid)"/> boundary.</para>
    /// <para>Uses the same signature, key, lifetime and replay-identifier validation as
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/>. The caller must durably prevent replay before
    /// accepting the appeal.</para>
    /// </remarks>
    public static Task<ModerationReportAuthenticationResult> VerifyModerationAppealAsync(
        string token,
        Did labeler,
        CancellationToken cancellationToken)
    {
        return VerifyModerationAppealAsync(
            token: token,
            labeler: labeler,
            timeProvider: null,
            maximumLifetime: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Verifies an authenticated Ozone appeal submission addressed to an independent labeler.
    /// </summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="timeProvider">The clock used to validate the lifetime, or <see langword="null"/> to use the system clock.</param>
    /// <param name="maximumLifetime">The maximum accepted service-token lifetime, or <see langword="null"/> to use the default of 15 minutes.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The authenticated reporter and replay identifiers, not an administrative caller.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Requires the <c>tools.ozone.inbox.appealActionedSubject</c> method claim and accepts either the exact
    /// <c>labeler#atproto_labeler</c> audience or the same bare labeler DID used by PDS proxies.
    /// This intake verifier grants no administrative authority, and
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>
    /// remains exact-audience. Ozone administration uses the separate
    /// <see cref="VerifyOzoneAdministrationAsync(string, Did, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/> boundary.</para>
    /// <para>Uses the same signature, key, lifetime and replay-identifier validation as
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>.
    /// The caller must durably prevent replay before accepting the appeal.</para>
    /// </remarks>
    public static async Task<ModerationReportAuthenticationResult> VerifyModerationAppealAsync(
        string token,
        Did labeler,
        TimeProvider? timeProvider,
        TimeSpan? maximumLifetime,
        CancellationToken cancellationToken)
    {
        using HttpClientLease lease = new(httpClient: null, timeout: null);
        return await VerifyModerationAppealAsync(
            token: token,
            labeler: labeler,
            httpClient: lease.Client,
            timeProvider: timeProvider,
            maximumLifetime: maximumLifetime,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies an authenticated Ozone appeal submission addressed to an independent labeler.
    /// </summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="httpClient">The HTTP client used to resolve the reporter's DID document.</param>
    /// <param name="timeProvider">An optional clock used to validate the lifetime.</param>
    /// <param name="maximumLifetime">The maximum accepted service-token lifetime. The default is 15 minutes.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The authenticated reporter and replay identifiers, not an administrative caller.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>Requires the <c>tools.ozone.inbox.appealActionedSubject</c> method claim and accepts either the exact
    /// <c>labeler#atproto_labeler</c> audience or the same bare labeler DID used by PDS proxies.
    /// This intake verifier grants no administrative authority, and
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>
    /// remains exact-audience. Ozone administration uses the separate
    /// <see cref="VerifyOzoneAdministrationAsync(string, Did, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/> boundary.</para>
    /// <para>Uses the same signature, key, lifetime and replay-identifier validation as
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>.
    /// The caller must durably prevent replay before accepting the appeal.</para>
    /// <para>This method uses <paramref name="httpClient"/> as supplied, adds no SSRF protection and does not dispose
    /// it. An attacker can choose a <c>did:web</c> issuer whose hostname resolves to loopback, link-local or private
    /// addresses, including cloud metadata endpoints. The handler must check resolved IP addresses at connection time
    /// and reject those destinations; checking a hostname before connecting does not prevent DNS rebinding. Disable
    /// automatic redirects, which can send a request to an unchecked destination.</para>
    /// <para>Register an <see cref="IHttpClientFactory"/> with
    /// <see cref="ServiceCollectionExtensions.AddAtProtoHttpClient(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
    /// and call <c>factory.CreateClient("idunno.atproto")</c> for the library's protected client, or provide equivalent
    /// protections in a custom handler. Calling <c>factory.CreateClient()</c> does not select the named client.</para>
    /// </remarks>
    public static async Task<ModerationReportAuthenticationResult> VerifyModerationAppealAsync(
        string token,
        Did labeler,
        HttpClient httpClient,
        TimeProvider? timeProvider = null,
        TimeSpan? maximumLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(labeler);
        ServiceAuthenticationResult verified = await VerifyCoreAsync(
            token: token,
            audience: new(labeler, "atproto_labeler"),
            method: new("tools.ozone.inbox.appealActionedSubject"),
            httpClient: httpClient,
            timeProvider: timeProvider,
            maximumLifetime: maximumLifetime,
            allowLegacyModerationAudience: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new(verified);
    }

    /// <summary>Verifies a method-bound Ozone administration assertion for an independent labeler.</summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="method">The exact Ozone administration method receiving the assertion.</param>
    /// <returns>The verified caller evidence and identifiers for durable replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="method"/> is not a supported Ozone compatibility method or administration namespace.</exception>
    /// <exception cref="InvalidDataException">The token, claims, identity or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">Identity resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The HTTP request times out.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Accepts the exact <c>labeler#atproto_labeler</c> audience or the same bare labeler DID used by Ozone's
    /// PDS proxy. This does not change <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/> or accept
    /// native <c>dev.idunno.labeler.admin</c> assertions.</para>
    /// <para>Verification grants no permission. The trusted adapter must consume the replay identifiers
    /// through <c>ILabelAdministrationStore.AuthenticateServiceAsync</c> and check live team authority
    /// for every operation. Public report and appeal intake must use their separate verification methods.</para>
    /// <para>The stock Ozone UI also requires the exact read methods <c>app.bsky.actor.getProfile</c>,
    /// <c>app.bsky.feed.getPostThread</c> and <c>tools.ozone.setting.listOptions</c>, and the settings procedure
    /// <c>tools.ozone.setting.upsertOption</c>. These are accepted without granting other AppView or settings methods,
    /// or relaxing the method claim.</para>
    /// </remarks>
    public static Task<ServiceAuthenticationResult> VerifyOzoneAdministrationAsync(
        string token,
        Did labeler,
        Nsid method)
    {
        return VerifyOzoneAdministrationAsync(
            token: token,
            labeler: labeler,
            method: method,
            cancellationToken: default);
    }

    /// <summary>Verifies a method-bound Ozone administration assertion for an independent labeler.</summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="method">The exact Ozone administration method receiving the assertion.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The verified caller evidence and identifiers for durable replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="method"/> is not a supported Ozone compatibility method or administration namespace.</exception>
    /// <exception cref="InvalidDataException">The token, claims, identity or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">Identity resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled or the HTTP request times out.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Accepts the exact <c>labeler#atproto_labeler</c> audience or the same bare labeler DID used by Ozone's
    /// PDS proxy. This does not change <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid)"/> or accept
    /// native <c>dev.idunno.labeler.admin</c> assertions.</para>
    /// <para>Verification grants no permission. The trusted adapter must consume the replay identifiers
    /// through <c>ILabelAdministrationStore.AuthenticateServiceAsync</c> and check live team authority
    /// for every operation. Public report and appeal intake must use their separate verification methods.</para>
    /// <para>The stock Ozone UI also requires the exact read methods <c>app.bsky.actor.getProfile</c>,
    /// <c>app.bsky.feed.getPostThread</c> and <c>tools.ozone.setting.listOptions</c>, and the settings procedure
    /// <c>tools.ozone.setting.upsertOption</c>. These are accepted without granting other AppView or settings methods,
    /// or relaxing the method claim.</para>
    /// </remarks>
    public static Task<ServiceAuthenticationResult> VerifyOzoneAdministrationAsync(
        string token,
        Did labeler,
        Nsid method,
        CancellationToken cancellationToken)
    {
        return VerifyOzoneAdministrationAsync(
            token: token,
            labeler: labeler,
            method: method,
            timeProvider: null,
            maximumLifetime: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>Verifies a method-bound Ozone administration assertion for an independent labeler.</summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="method">The exact Ozone administration method receiving the assertion.</param>
    /// <param name="timeProvider">The validation clock, or <see langword="null"/> to use the system clock.</param>
    /// <param name="maximumLifetime">The maximum accepted service-token lifetime, or <see langword="null"/> to use the default of 15 minutes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The verified caller evidence and identifiers for durable replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="method"/> is not a supported Ozone compatibility method or administration namespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, identity or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">Identity resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>This overload uses an SSRF-protected client that rejects loopback, link-local and private destinations
    /// at connection time and does not follow redirects.</para>
    /// <para>Accepts the exact <c>labeler#atproto_labeler</c> audience or the same bare labeler DID
    /// used by Ozone's PDS proxy. This does not change
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>
    /// or accept native <c>dev.idunno.labeler.admin</c> assertions.</para>
    /// <para>Verification grants no permission. The trusted adapter must consume the replay identifiers
    /// through <c>ILabelAdministrationStore.AuthenticateServiceAsync</c> and check live team authority
    /// for every operation. Public report and appeal intake must use their separate verification methods.</para>
    /// <para>The stock Ozone UI also requires the exact read methods <c>app.bsky.actor.getProfile</c>,
    /// <c>app.bsky.feed.getPostThread</c> and <c>tools.ozone.setting.listOptions</c>, and the settings procedure
    /// <c>tools.ozone.setting.upsertOption</c>. These are accepted without granting other AppView or settings methods,
    /// or relaxing the method claim.</para>
    /// </remarks>
    public static async Task<ServiceAuthenticationResult> VerifyOzoneAdministrationAsync(
        string token,
        Did labeler,
        Nsid method,
        TimeProvider? timeProvider,
        TimeSpan? maximumLifetime,
        CancellationToken cancellationToken)
    {
        using HttpClientLease lease = new(httpClient: null, timeout: null);
        return await VerifyOzoneAdministrationAsync(
            token: token,
            labeler: labeler,
            method: method,
            httpClient: lease.Client,
            timeProvider: timeProvider,
            maximumLifetime: maximumLifetime,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verifies a method-bound Ozone administration assertion for an independent labeler.</summary>
    /// <param name="token">The compact signed service JWT.</param>
    /// <param name="labeler">The destination labeler DID.</param>
    /// <param name="method">The exact Ozone administration method receiving the assertion.</param>
    /// <param name="httpClient">The HTTP client used to resolve the caller's DID document.</param>
    /// <param name="timeProvider">An optional validation clock.</param>
    /// <param name="maximumLifetime">The maximum accepted service-token lifetime. The default is 15 minutes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The verified caller evidence and identifiers for durable replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="method"/> is not a supported Ozone compatibility method or administration namespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, identity or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">Identity resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>Accepts the exact <c>labeler#atproto_labeler</c> audience or the same bare labeler DID
    /// used by Ozone's PDS proxy. This does not change
    /// <see cref="VerifyAsync(string, AtProtoServiceReference, Nsid, TimeProvider?, TimeSpan?, CancellationToken)"/>
    /// or accept native <c>dev.idunno.labeler.admin</c> assertions.</para>
    /// <para>Verification grants no permission. The trusted adapter must consume the replay identifiers
    /// through <c>ILabelAdministrationStore.AuthenticateServiceAsync</c> and check live team authority
    /// for every operation. Public report and appeal intake must use their separate verification methods.</para>
    /// <para>The stock Ozone UI also requires the exact read methods <c>app.bsky.actor.getProfile</c>,
    /// <c>app.bsky.feed.getPostThread</c> and <c>tools.ozone.setting.listOptions</c>, and the settings procedure
    /// <c>tools.ozone.setting.upsertOption</c>. These are accepted without granting other AppView or settings methods,
    /// or relaxing the method claim.</para>
    /// <para>This method uses <paramref name="httpClient"/> as supplied, adds no SSRF protection and does not dispose
    /// it. An attacker can choose a <c>did:web</c> issuer whose hostname resolves to loopback, link-local or private
    /// addresses, including cloud metadata endpoints. The handler must check resolved IP addresses at connection time
    /// and reject those destinations; checking a hostname before connecting does not prevent DNS rebinding. Disable
    /// automatic redirects, which can send a request to an unchecked destination.</para>
    /// <para>Register an <see cref="IHttpClientFactory"/> with
    /// <see cref="ServiceCollectionExtensions.AddAtProtoHttpClient(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
    /// and call <c>factory.CreateClient("idunno.atproto")</c> for the library's protected client, or provide equivalent
    /// protections in a custom handler. Calling <c>factory.CreateClient()</c> does not select the named client.</para>
    /// </remarks>
    public static Task<ServiceAuthenticationResult> VerifyOzoneAdministrationAsync(
        string token,
        Did labeler,
        Nsid method,
        HttpClient httpClient,
        TimeProvider? timeProvider = null,
        TimeSpan? maximumLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(labeler);
        ArgumentNullException.ThrowIfNull(method);
        string nsid = method.ToString();
        if (!nsid.StartsWith("tools.ozone.server.", StringComparison.Ordinal) &&
            !nsid.StartsWith("tools.ozone.team.", StringComparison.Ordinal) &&
            !nsid.StartsWith("tools.ozone.moderation.", StringComparison.Ordinal) &&
            !nsid.StartsWith("tools.ozone.report.", StringComparison.Ordinal) &&
            nsid is not ("app.bsky.actor.getProfile" or "app.bsky.feed.getPostThread" or
                "tools.ozone.setting.listOptions" or "tools.ozone.setting.upsertOption"))
        {
            throw new ArgumentException("An Ozone administration method is required.", nameof(method));
        }

        return VerifyCoreAsync(
            token: token,
            audience: new(labeler, "atproto_labeler"),
            method: method,
            httpClient: httpClient,
            timeProvider: timeProvider,
            maximumLifetime: maximumLifetime,
            allowLegacyModerationAudience: true,
            cancellationToken: cancellationToken);
    }

    /// <summary>Applies common token validation with explicitly selected labeler audience compatibility.</summary>
    private static async Task<ServiceAuthenticationResult> VerifyCoreAsync(
        string token,
        AtProtoServiceReference audience,
        Nsid method,
        HttpClient httpClient,
        TimeProvider? timeProvider,
        TimeSpan? maximumLifetime,
        bool allowLegacyModerationAudience,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(httpClient);
        cancellationToken.ThrowIfCancellationRequested();
        TimeSpan lifetimeLimit = maximumLifetime ?? s_defaultMaximumLifetime;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetimeLimit, TimeSpan.Zero);

        string failureCategory = "token-malformed";
        try
        {
            if (token.Length is 0 or > MaximumTokenLength)
            {
                throw new InvalidDataException("The service token exceeds the supported size or is empty.");
            }

            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                throw new InvalidDataException("The service token must be a compact signed JWT.");
            }

            byte[] headerBytes = DecodeSegment(parts[0]);
            byte[] payloadBytes = DecodeSegment(parts[1]);
            byte[] signature = DecodeSegment(parts[2]);
            if (signature.Length != 64)
            {
                throw new InvalidDataException("The service token signature must contain 64 bytes.");
            }

            using JsonDocument headerDocument = JsonDocument.Parse(headerBytes, new JsonDocumentOptions { MaxDepth = 16 });
            using JsonDocument payloadDocument = JsonDocument.Parse(payloadBytes, new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement header = headerDocument.RootElement;
            JsonElement payload = payloadDocument.RootElement;
            ValidateObject(header);
            ValidateObject(payload);

            failureCategory = "token-header-invalid";
            if (ReadString(header, "typ") != "JWT" ||
                ReadString(header, "alg") is not ("ES256" or "ES256K") ||
                (header.TryGetProperty("kid", out JsonElement kid) && (kid.ValueKind != JsonValueKind.String || kid.GetString() != "#atproto")) ||
                header.TryGetProperty("jku", out _) || header.TryGetProperty("x5u", out _) ||
                header.TryGetProperty("jwk", out _) || header.TryGetProperty("crit", out _) ||
                header.TryGetProperty("b64", out _))
            {
                throw new InvalidDataException("The service token contains an unsupported JOSE header.");
            }

            JsonWebTokenHandler handler = new() { MaximumTokenSizeInBytes = MaximumTokenLength };
            JsonWebToken jwt = handler.ReadJsonWebToken(token);
            failureCategory = "token-audience-mismatch";
            string tokenAudience = ReadString(payload, "aud");
            bool legacyModerationAudience = allowLegacyModerationAudience &&
                audience.Fragment == "atproto_labeler" &&
                tokenAudience == audience.Did.Value;
            if (jwt.IsEncrypted ||
                (tokenAudience != audience.ToString() && !legacyModerationAudience))
            {
                throw new InvalidDataException("The service token audience does not match the request.");
            }

            failureCategory = "token-method-mismatch";
            if (ReadString(payload, "lxm") != method.ToString())
            {
                throw new InvalidDataException("The service token method does not match the request.");
            }

            failureCategory = "token-issuer-invalid";
            string issuer = ReadString(payload, "iss");
            if (!Did.TryParse(issuer, out Did? did))
            {
                throw new InvalidDataException("The service token issuer must be a bare DID.");
            }

            failureCategory = "token-replay-identifier-invalid";
            string tokenId = ReadString(payload, "jti");
            if (string.IsNullOrWhiteSpace(tokenId) || tokenId.Length > 256)
            {
                throw new InvalidDataException("The service token identifier must contain between 1 and 256 nonblank characters.");
            }

            failureCategory = "token-lifetime-invalid";
            DateTimeOffset issuedAt = ReadTimestamp(payload, "iat");
            DateTimeOffset expiresAt = ReadTimestamp(payload, "exp");
            DateTimeOffset? notBefore = payload.TryGetProperty("nbf", out _) ? ReadTimestamp(payload, "nbf") : null;
            DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();
            ValidateLifetime(issuedAt: issuedAt, expiresAt: expiresAt, notBefore: notBefore, now: now, maximumLifetime: lifetimeLimit);

            DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
                did: did,
                httpClient: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            SigningKey key;
            try
            {
                key = SigningKeyVerifier.GetKey(document, did, SigningKeyVerifier.RepositorySigningKeyFragment);
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or CryptographicException)
            {
                InvalidDataException failure = new("The issuer's service signing key could not be verified.", exception);
                failure.Data["ServiceAuthenticationFailure"] = "token-signing-key-invalid";
                throw failure;
            }

            failureCategory = "token-algorithm-mismatch";
            if (key.JwtAlgorithm != jwt.Alg)
            {
                throw new InvalidDataException("The service token algorithm does not match the issuer's signing key.");
            }

            failureCategory = "token-signature-invalid";
            key.Verify(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, "service token");
            ValidateLifetime(
                issuedAt: issuedAt,
                expiresAt: expiresAt,
                notBefore: notBefore,
                now: (timeProvider ?? TimeProvider.System).GetUtcNow(),
                maximumLifetime: lifetimeLimit);

            return new ServiceAuthenticationResult(
                issuer: did,
                audience: audience,
                tokenId: tokenId,
                expiresAt: expiresAt,
                tokenHash: Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))))
            {
                TokenAudience = tokenAudience
            };
        }
        catch (InvalidDataException exception)
        {
            // Share only a server-selected category with transport logging, never claim or exception text.
            exception.Data["ServiceAuthenticationFailure"] ??= failureCategory;
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or CryptographicException or SecurityTokenException)
        {
            InvalidDataException failure = new("The service token could not be verified.", exception);
            failure.Data["ServiceAuthenticationFailure"] = failureCategory;
            throw failure;
        }
    }

    /// <summary>
    /// Decodes a canonical, unpadded base64url JWT segment.
    /// </summary>
    private static byte[] DecodeSegment(string segment)
    {
        if (segment.Length == 0 || segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new InvalidDataException("The service token contains an invalid base64url segment.");
        }

        string padded = segment.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
        byte[] bytes = Convert.FromBase64String(padded);
        if (Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != segment)
        {
            throw new InvalidDataException("The service token contains a noncanonical base64url segment.");
        }

        return bytes;
    }

    /// <summary>
    /// Rejects duplicate properties and invalid Unicode anywhere in a JWT JSON object.
    /// </summary>
    private static void ValidateObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("JWT headers and claims must be JSON objects.");
        }

        try
        {
            HashSet<string> names = [with(StringComparer.Ordinal)];
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("The service token contains duplicate JSON properties.");
                }

                ValidateChildren(property.Value);
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The service token contains invalid JSON text.", exception);
        }
    }

    /// <summary>
    /// Traverses nested JSON values to validate object properties and force Unicode decoding of every string.
    /// </summary>
    private static void ValidateChildren(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(element);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                ValidateChildren(child);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            _ = element.GetString();
        }
    }

    /// <summary>
    /// Reads a required scalar string without coercing JSON values.
    /// </summary>
    private static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"The service token requires a scalar '{name}' string.");
        }

        return value.GetString()!;
    }

    /// <summary>
    /// Reads a required finite integer timestamp within the supported date range.
    /// </summary>
    private static DateTimeOffset ReadTimestamp(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long seconds))
        {
            throw new InvalidDataException($"The service token requires an integer '{name}' timestamp.");
        }

        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    /// <summary>
    /// Enforces the bounded lifetime before and after DID resolution.
    /// </summary>
    private static void ValidateLifetime(
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? notBefore,
        DateTimeOffset now,
        TimeSpan maximumLifetime)
    {
        TimeSpan lifetime = expiresAt - issuedAt;
        if (lifetime <= TimeSpan.Zero || lifetime > maximumLifetime ||
            issuedAt - now > TimeSpan.FromSeconds(5) || now - expiresAt > TimeSpan.FromSeconds(5) ||
            notBefore is { } earliest && (earliest >= expiresAt || earliest - now > TimeSpan.FromSeconds(5)))
        {
            InvalidDataException failure = new("The service token lifetime is invalid or has expired.");
            string category = "token-lifetime-invalid";
            if (now - expiresAt > TimeSpan.FromSeconds(5))
            {
                category = "token-expired";
            }
            else if (issuedAt - now > TimeSpan.FromSeconds(5) || notBefore is { } start && start - now > TimeSpan.FromSeconds(5))
            {
                category = "token-not-yet-valid";
            }

            failure.Data["ServiceAuthenticationFailure"] = category;
            throw failure;
        }
    }
}
