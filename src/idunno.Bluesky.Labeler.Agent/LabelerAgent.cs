// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Agent.Errors;

namespace idunno.Bluesky.Labeler.Agent;

/// <summary>Calls a remote labeler host using method-bound service authentication.</summary>
/// <remarks>
/// <para>The account agent is borrowed or owned according to the builder configuration. Protected administration
/// calls require an active authenticated session; public applied-label queries do not forward account credentials.</para>
/// </remarks>
public sealed class LabelerAgent : IDisposable
{
    private const int DefaultMaximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize;
    private const string IssuanceNamespace = "dev.idunno.labeler.issuance.";
    private static readonly List<Func<AtErrorDetail?, AtErrorDetail?>> s_errorMappers = [LabelerAgentError.MapLabelerError];

    private readonly bool _ownsAccountAgent;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="LabelerAgent"/> class.</summary>
    /// <param name="accountAgent">The authenticated account agent.</param>
    /// <param name="labelerService">The HTTPS origin of the labeler host.</param>
    /// <param name="audience">The labeler's issuance service reference.</param>
    /// <param name="ownsAccountAgent"><see langword="true"/> to dispose the account agent with this instance; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="labelerService"/> is not a root HTTPS URI without credentials, query, or fragment.</exception>
    /// <exception cref="AuthenticationRequiredException"><paramref name="ownsAccountAgent"/> is <see langword="false"/> and <paramref name="accountAgent"/> is unauthenticated.</exception>
    internal LabelerAgent(AtProtoAgent accountAgent, Uri labelerService, AtProtoServiceReference audience, bool ownsAccountAgent)
    {
        ArgumentNullException.ThrowIfNull(accountAgent);
        ArgumentNullException.ThrowIfNull(labelerService);
        ArgumentNullException.ThrowIfNull(audience);
        if (!labelerService.IsAbsoluteUri ||
            labelerService.Scheme != Uri.UriSchemeHttps ||
            labelerService.AbsolutePath != "/" ||
            labelerService.UserInfo.Length != 0 ||
            labelerService.Query.Length != 0 ||
            labelerService.Fragment.Length != 0)
        {
            throw new ArgumentException("The labeler service must be a root HTTPS URI without credentials, query, or fragment.", nameof(labelerService));
        }

        if (!ownsAccountAgent && !accountAgent.IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AccountAgent = accountAgent;
        LabelerService = labelerService;
        Audience = audience;
        _ownsAccountAgent = ownsAccountAgent;
    }

    /// <summary>Gets the account agent used for login, logout and service-token acquisition.</summary>
    /// <value>The borrowed or owned agent.</value>
    public AtProtoAgent AccountAgent { get; }

    /// <summary>Gets the configured labeler host URI.</summary>
    /// <value>The root HTTPS URI.</value>
    public Uri LabelerService { get; }

    /// <summary>Gets the exact issuance service-token audience configured for the labeler host.</summary>
    /// <value>The service DID and issuance fragment.</value>
    public AtProtoServiceReference Audience { get; }

    /// <summary>Issues a label through the remote labeler.</summary>
    /// <param name="operationId">The durable identifier to reuse when recovering an uncertain result.</param>
    /// <param name="target">The account or record being labeled.</param>
    /// <param name="value">The declared label value.</param>
    /// <param name="reason">The nonblank administrative reason.</param>
    /// <param name="expiresAt">An optional absolute expiry for the label.</param>
    /// <param name="correlationId">An optional diagnostic correlation identifier.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The HTTP result containing the durable operation receipt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required text argument is blank.</exception>
    /// <exception cref="AuthenticationRequiredException">The account agent is not authenticated.</exception>
    /// <exception cref="ObjectDisposedException">This labeler agent has been disposed.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overload without an operation identifier creates one automatically.")]
    public Task<AtProtoHttpResult<LabelOperationReceipt>> IssueLabel(
        string operationId,
        LabelTarget target,
        string value,
        string reason,
        DateTimeOffset? expiresAt = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return MutateLabel(
            method: "issueLabel",
            operationId: operationId,
            target: target,
            value: value,
            reason: reason,
            expiresAt: expiresAt,
            correlationId: correlationId,
            cancellationToken: cancellationToken);
    }

    /// <summary>Issues a label using a newly generated operation identifier.</summary>
    /// <param name="target">The account or record being labeled.</param>
    /// <param name="value">The declared label value.</param>
    /// <param name="reason">The nonblank administrative reason.</param>
    /// <param name="expiresAt">An optional absolute expiry for the label.</param>
    /// <param name="correlationId">An optional diagnostic correlation identifier.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The HTTP result containing the durable operation receipt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required text argument is blank.</exception>
    /// <exception cref="AuthenticationRequiredException">The account agent is not authenticated.</exception>
    /// <exception cref="ObjectDisposedException">This labeler agent has been disposed.</exception>
    /// <remarks>
    /// <para>The generated identifier is unique to this call. If the response is lost, a later call
    /// generates a different identifier and cannot recover this operation's outcome. Supply and
    /// persist your own identifier when you need to retry safely or recover the receipt after an
    /// uncertain result.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overload omits the operation identifier and creates one automatically.")]
    public Task<AtProtoHttpResult<LabelOperationReceipt>> IssueLabel(
        LabelTarget target,
        string value,
        string reason,
        DateTimeOffset? expiresAt = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        => IssueLabel(
            operationId: Guid.NewGuid().ToString("N"),
            target: target,
            value: value,
            reason: reason,
            expiresAt: expiresAt,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

    /// <summary>Negates a label through the remote labeler.</summary>
    /// <param name="operationId">The durable identifier to reuse when recovering an uncertain result.</param>
    /// <param name="target">The account or record whose label is being negated.</param>
    /// <param name="value">The declared label value to negate.</param>
    /// <param name="reason">The nonblank administrative reason.</param>
    /// <param name="correlationId">An optional diagnostic correlation identifier.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The HTTP result containing the durable operation receipt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required text argument is blank.</exception>
    /// <exception cref="AuthenticationRequiredException">The account agent is not authenticated.</exception>
    /// <exception cref="ObjectDisposedException">This labeler agent has been disposed.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overload without an operation identifier creates one automatically.")]
    public Task<AtProtoHttpResult<LabelOperationReceipt>> NegateLabel(
        string operationId,
        LabelTarget target,
        string value,
        string reason,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return MutateLabel(
            method: "negateLabel",
            operationId: operationId,
            target: target,
            value: value,
            reason: reason,
            expiresAt: null,
            correlationId: correlationId,
            cancellationToken: cancellationToken);
    }

    /// <summary>Negates a label using a newly generated operation identifier.</summary>
    /// <param name="target">The account or record whose label is being negated.</param>
    /// <param name="value">The declared label value to negate.</param>
    /// <param name="reason">The nonblank administrative reason.</param>
    /// <param name="correlationId">An optional diagnostic correlation identifier.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The HTTP result containing the durable operation receipt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required text argument is blank.</exception>
    /// <exception cref="AuthenticationRequiredException">The account agent is not authenticated.</exception>
    /// <exception cref="ObjectDisposedException">This labeler agent has been disposed.</exception>
    /// <remarks>
    /// <para>The generated identifier is unique to this call. If the response is lost, a later call
    /// generates a different identifier and cannot recover this operation's outcome. Supply and
    /// persist your own identifier when you need to retry safely or recover the receipt after an
    /// uncertain result.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overload omits the operation identifier and creates one automatically.")]
    public Task<AtProtoHttpResult<LabelOperationReceipt>> NegateLabel(
        LabelTarget target,
        string value,
        string reason,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        => NegateLabel(
            operationId: Guid.NewGuid().ToString("N"),
            target: target,
            value: value,
            reason: reason,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

    /// <summary>Recovers the durable outcome of a prior issue or negate operation.</summary>
    /// <param name="operationId">The original durable operation identifier.</param>
    /// <param name="reason">The administrative inspection reason.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The HTTP result containing the durable operation receipt.</returns>
    /// <exception cref="ArgumentException"><paramref name="operationId"/> or <paramref name="reason"/> is blank.</exception>
    /// <exception cref="AuthenticationRequiredException">The account agent is not authenticated.</exception>
    /// <exception cref="ObjectDisposedException">This labeler agent has been disposed.</exception>
    public async Task<AtProtoHttpResult<LabelOperationReceipt>> GetLabelOperationReceipt(
        string operationId,
        string reason = "Administrative inspection.",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ObjectDisposedException.ThrowIf(_disposed, this);
        AtProtoHttpResult<ServiceCredential> credential = await GetServiceCredential(
            "getOperationReceipt",
            cancellationToken).ConfigureAwait(false);
        if (!credential.Succeeded)
        {
            return CopyResult<LabelOperationReceipt>(credential);
        }

        string endpoint = "/xrpc/" + IssuanceNamespace + "getOperationReceipt" +
            "?operationId=" + Uri.EscapeDataString(operationId) +
            "&reason=" + Uri.EscapeDataString(reason);
        AtProtoHttpResult<AdministrationResponse> response = await SendGet(
            endpoint: endpoint,
            credentials: credential.Result,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return MapReceipt(response);
    }

    /// <summary>Gets the label values declared by the labeler's current issuance policy.</summary>
    /// <param name="reason">The administrative inspection reason.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The HTTP result containing the currently available label values.</returns>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is blank.</exception>
    /// <exception cref="InvalidDataException">The labeler returned an incomplete available-label response.</exception>
    /// <exception cref="AuthenticationRequiredException">The account agent is not authenticated.</exception>
    /// <exception cref="ObjectDisposedException">This labeler agent has been disposed.</exception>
    public async Task<AtProtoHttpResult<IReadOnlyList<string>>> GetAvailableLabels(
        string reason = "Administrative inspection.",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ObjectDisposedException.ThrowIf(_disposed, this);
        AtProtoHttpResult<ServiceCredential> credential = await GetServiceCredential(
            "getAvailableLabels",
            cancellationToken).ConfigureAwait(false);
        if (!credential.Succeeded)
        {
            return CopyResult<IReadOnlyList<string>>(credential);
        }

        string endpoint = "/xrpc/" + IssuanceNamespace + "getAvailableLabels?reason=" + Uri.EscapeDataString(reason);
        AtProtoHttpResult<AdministrationResponse> response = await SendGet(
            endpoint: endpoint,
            credentials: credential.Result,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!response.Succeeded)
        {
            return CopyResult<IReadOnlyList<string>>(response);
        }

        string[] labels = response.Result.Moderation?.LabelChoices?.Select(choice => choice.Value).ToArray()
            ?? throw new InvalidDataException("The labeler returned no available-label list.");
        return new AtProtoHttpResult<IReadOnlyList<string>>(
            new ReadOnlyCollection<string>(labels),
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }

    /// <summary>Queries the labeler's public distribution endpoint for labels on an account DID.</summary>
    /// <param name="subject">The account DID.</param>
    /// <param name="limit">The maximum number of matching labels to return, from 1 through 250.</param>
    /// <param name="cursor">An optional continuation from a previous page.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The HTTP result containing the matching labels and continuation cursor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside the range 1 through 250.</exception>
    /// <exception cref="ObjectDisposedException">This labeler agent has been disposed.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overloads distinguish account DIDs from record AT URIs.")]
    public Task<AtProtoHttpResult<PagedReadOnlyCollection<Label>>> GetAppliedLabels(
        Did subject,
        int? limit = 100,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return GetAppliedLabels(subject.Value, limit, cursor, cancellationToken);
    }

    /// <summary>Queries the labeler's public distribution endpoint for labels on an AT Protocol record.</summary>
    /// <param name="subject">The record AT URI.</param>
    /// <param name="limit">The maximum number of matching labels to return, from 1 through 250.</param>
    /// <param name="cursor">An optional continuation from a previous page.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The HTTP result containing the matching labels and continuation cursor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="subject"/> does not identify a complete record.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside the range 1 through 250.</exception>
    /// <exception cref="ObjectDisposedException">This labeler agent has been disposed.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overloads distinguish account DIDs from record AT URIs.")]
    public Task<AtProtoHttpResult<PagedReadOnlyCollection<Label>>> GetAppliedLabels(
        AtUri subject,
        int? limit = 100,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.Collection is null || subject.RecordKey is null)
        {
            throw new ArgumentException("A record locator must contain a collection and record key.", nameof(subject));
        }

        return GetAppliedLabels(subject.ToString(), limit, cursor, cancellationToken);
    }

    /// <summary>Disposes the account agent only when this instance owns it.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsAccountAgent)
        {
            AccountAgent.Dispose();
        }

        _disposed = true;
    }

    /// <summary>Issues or negates one label using a new single-use service credential.</summary>
    private async Task<AtProtoHttpResult<LabelOperationReceipt>> MutateLabel(
        string method,
        string operationId,
        LabelTarget target,
        string value,
        string reason,
        DateTimeOffset? expiresAt,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AtProtoHttpResult<ServiceCredential> credential = await GetServiceCredential(method, cancellationToken).ConfigureAwait(false);
        if (!credential.Succeeded)
        {
            return CopyResult<LabelOperationReceipt>(credential);
        }

        AdministrationCommand command = new(
            OperationId: operationId,
            Reason: reason,
            Target: target.Kind == LabelTargetKind.Record ? target.RecordUri!.ToString() : target.Repository.Value,
            Value: value,
            Cid: target.Cid?.ToString(),
            ExpiresAt: expiresAt,
            CorrelationId: correlationId);
        AtProtoHttpResult<AdministrationResponse> response = await SendPost(method, command, credential.Result, cancellationToken).ConfigureAwait(false);
        return MapReceipt(response);
    }

    /// <summary>Gets a service credential bound to the exact administration method.</summary>
    private Task<AtProtoHttpResult<ServiceCredential>> GetServiceCredential(string method, CancellationToken cancellationToken)
    {
        if (!AccountAgent.IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return AccountAgent.GetServiceAuth(
            AccountAgent.Service,
            Audience,
            new Nsid(IssuanceNamespace + method),
            cancellationToken: cancellationToken);
    }

    /// <summary>Sends one bounded authenticated GET using source-generated JSON metadata.</summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The response types are registered in LabelerAgentJsonContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling",
        Justification = "The response types are registered in LabelerAgentJsonContext.")]
    private Task<AtProtoHttpResult<AdministrationResponse>> SendGet(
        string endpoint,
        ServiceCredential credentials,
        CancellationToken cancellationToken)
    {
        AtProtoHttpClient<AdministrationResponse> client = new(
            serviceProxy: null,
            requestHeaders: null,
            loggerFactory: null,
            meterFactory: null,
            errorMappers: s_errorMappers)
        {
            MaximumResponseSize = DefaultMaximumResponseSize
        };
        return client.Get(
            service: LabelerService,
            endpoint: endpoint,
            credentials: credentials,
            httpClient: AccountAgent.HttpClient,
            onCredentialsUpdated: null,
            jsonSerializerOptions: LabelerAgentJsonContext.Default.Options,
            cancellationToken: cancellationToken);
    }

    /// <summary>Sends one bounded authenticated POST using source-generated JSON metadata.</summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "Request and response types are registered in LabelerAgentJsonContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling",
        Justification = "Request and response types are registered in LabelerAgentJsonContext.")]
    private Task<AtProtoHttpResult<AdministrationResponse>> SendPost(
        string method,
        AdministrationCommand command,
        ServiceCredential credentials,
        CancellationToken cancellationToken)
    {
        AtProtoHttpClient<AdministrationResponse> client = new(
            serviceProxy: null,
            requestHeaders: null,
            loggerFactory: null,
            meterFactory: null,
            errorMappers: s_errorMappers)
        {
            MaximumResponseSize = DefaultMaximumResponseSize
        };
        return client.Post(
            service: LabelerService,
            endpoint: "/xrpc/" + IssuanceNamespace + method,
            record: command,
            credentials: credentials,
            httpClient: AccountAgent.HttpClient,
            onCredentialsUpdated: null,
            cancellationToken: cancellationToken,
            jsonSerializerOptions: LabelerAgentJsonContext.Default.Options);
    }

    /// <summary>Queries the anonymous public label distribution API without forwarding account credentials.</summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "AtProtoServer.QueryLabels uses its source-generated protocol serializer.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling",
        Justification = "AtProtoServer.QueryLabels uses its source-generated protocol serializer.")]
    private async Task<AtProtoHttpResult<PagedReadOnlyCollection<Label>>> GetAppliedLabels(
        string subject,
        int? limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, 250);
        }

        return await AtProtoServer.QueryLabels(
            uriPatterns: [subject],
            sources: [Audience.Did],
            limit: limit,
            cursor: cursor,
            service: LabelerService,
            accessCredentials: null,
            httpClient: AccountAgent.HttpClient,
            loggerFactory: null,
            maximumResponseSize: DefaultMaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Projects an administration response into its durable operation receipt.</summary>
    private static AtProtoHttpResult<LabelOperationReceipt> MapReceipt(AtProtoHttpResult<AdministrationResponse> response)
    {
        if (!response.Succeeded)
        {
            return CopyResult<LabelOperationReceipt>(response);
        }

        if (response.Result.OperationId is null || response.Result.Changed is null)
        {
            throw new InvalidDataException("The labeler returned an incomplete operation receipt.");
        }

        return new AtProtoHttpResult<LabelOperationReceipt>(
            new LabelOperationReceipt(response.Result.OperationId, response.Result.Sequence, response.Result.Changed.Value),
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }

    /// <summary>Copies an unsuccessful HTTP result without changing its transport diagnostics.</summary>
    private static AtProtoHttpResult<T> CopyResult<T>(AtProtoHttpResult<ServiceCredential> response)
        where T : class
        => new(null, response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail, response.RateLimit);

    /// <summary>Copies an unsuccessful administration response without changing its transport diagnostics.</summary>
    private static AtProtoHttpResult<T> CopyResult<T>(AtProtoHttpResult<AdministrationResponse> response)
        where T : class
        => new(null, response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail, response.RateLimit);
}
