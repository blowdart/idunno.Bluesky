// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;

namespace idunno.Bluesky;

/// <summary>Identifies the last completed stage of a PLC labeler identity update.</summary>
public enum PlcLabelerIdentityUpdateStage
{
    /// <summary>The operation could not be signed by the authenticated PDS.</summary>
    SigningFailed,
    /// <summary>The operation was signed but could not be submitted; retain it for recovery.</summary>
    SubmissionFailed,
    /// <summary>The operation was submitted but its publication could not be verified.</summary>
    VerificationFailed,
    /// <summary>The operation was submitted and the resolved DID document matches the requested identity.</summary>
    Verified
}

/// <summary>Describes the outcome of a PLC labeler identity update and any signed operation needed for recovery.</summary>
public sealed class PlcLabelerIdentityUpdateResult
{
    internal PlcLabelerIdentityUpdateResult(
        PlcLabelerIdentityUpdateStage stage,
        Did did,
        JsonElement? signedOperation,
        DidDocument? didDocument,
        HttpStatusCode? statusCode,
        AtErrorDetail? error,
        string? recoveryAction)
    {
        Stage = stage;
        Did = did;
        SignedOperation = signedOperation;
        DidDocument = didDocument;
        StatusCode = statusCode;
        Error = error;
        RecoveryAction = recoveryAction;
    }

    /// <summary>Gets the last completed workflow stage.</summary>
    public PlcLabelerIdentityUpdateStage Stage { get; }

    /// <summary>Gets the labeler's PLC DID.</summary>
    public Did Did { get; }

    /// <summary>Gets the signed PLC operation when signing succeeded.</summary>
    /// <remarks>
    /// <para>The operation remains a <see cref="JsonElement"/> instead of an SDK DTO so fields the SDK does not model are retained when the PDS-signed payload is recovered and resubmitted.</para>
    /// <para>This contains public DID data only. Preserve it securely as recovery data if submission did not complete.</para>
    /// </remarks>
    public JsonElement? SignedOperation { get; }

    /// <summary>Gets the resolved DID document when publication verification was attempted.</summary>
    public DidDocument? DidDocument { get; }

    /// <summary>Gets the HTTP status code from the failed PDS operation, if any.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Gets the protocol error returned by the PDS, if any.</summary>
    public AtErrorDetail? Error { get; }

    /// <summary>Gets the operator action needed to resume an incomplete update.</summary>
    public string? RecoveryAction { get; }
}

public partial class BlueskyAgent
{
    /// <summary>
    /// Requests an email code that can authorize a PLC operation signed by the account's personal data server.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The result of requesting the authorization code.</returns>
    /// <exception cref="AuthenticationRequiredException">The current session is not authenticated.</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The request has no body and EmptyResponse metadata is provided by the AT Protocol source-generation context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "The request has no body and EmptyResponse metadata is provided by the AT Protocol source-generation context.")]
    public async Task<AtProtoHttpResult<EmptyResponse>> RequestPlcOperationSignature(
        CancellationToken cancellationToken = default)
    {
        AccessCredentials credentials = Credentials ?? throw new AuthenticationRequiredException();

        AtProtoHttpClient<EmptyResponse> client = new(LoggerFactory)
        {
            MaximumResponseSize = MaximumResponseSize
        };

        return await client.Post(
            service: Service,
            endpoint: "/xrpc/com.atproto.identity.requestPlcOperationSignature",
            credentials: credentials,
            httpClient: HttpClient,
            jsonSerializerOptions: AtProtoJsonSerializerOptions.Default,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Submits a signed PLC operation through the authenticated account's PDS.
    /// </summary>
    /// <param name="signedOperation">The complete signed PLC operation returned by the PDS.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The result of submitting the operation.</returns>
    /// <exception cref="ArgumentException"><paramref name="signedOperation"/> is not an object containing a PLC operation signature.</exception>
    /// <exception cref="AuthenticationRequiredException">The current session is not authenticated.</exception>
    /// <remarks>
    /// <para>Use this to resume submission after <see cref="UpdatePlcLabelerIdentity"/> returns <see cref="PlcLabelerIdentityUpdateStage.SubmissionFailed"/>. Resolve the DID before retrying to avoid submitting an operation that already succeeded.</para>
    /// <para>The PLC operation is accepted as a <see cref="JsonElement"/> rather than an SDK DTO so unknown fields from the PDS-signed operation are preserved when forwarding it unchanged.</para>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The request has a JsonObject body and EmptyResponse metadata from the AT Protocol source-generation context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "The request has a JsonObject body and EmptyResponse metadata from the AT Protocol source-generation context.")]
    public async Task<AtProtoHttpResult<EmptyResponse>> SubmitPlcLabelerIdentityOperation(
        JsonElement signedOperation,
        CancellationToken cancellationToken = default)
    {
        bool validOperation =
            signedOperation.ValueKind == JsonValueKind.Object &&
            signedOperation.TryGetProperty("type", out JsonElement type) &&
            type.ValueKind == JsonValueKind.String &&
            type.GetString() == "plc_operation" &&
            signedOperation.TryGetProperty("sig", out JsonElement signature) &&
            signature.ValueKind == JsonValueKind.String &&
            !string.IsNullOrEmpty(signature.GetString()) &&
            signedOperation.TryGetProperty("verificationMethods", out JsonElement methods) &&
            methods.ValueKind == JsonValueKind.Object &&
            methods.TryGetProperty("atproto_label", out JsonElement labelKey) &&
            labelKey.ValueKind == JsonValueKind.String &&
            labelKey.GetString() is string labelKeyValue &&
            labelKeyValue.StartsWith("did:key:", StringComparison.Ordinal) &&
            signedOperation.TryGetProperty("services", out JsonElement services) &&
            services.ValueKind == JsonValueKind.Object &&
            services.TryGetProperty("atproto_labeler", out JsonElement labelerService) &&
            labelerService.ValueKind == JsonValueKind.Object &&
            labelerService.TryGetProperty("type", out JsonElement serviceType) &&
            serviceType.ValueKind == JsonValueKind.String &&
            serviceType.GetString() == "AtprotoLabeler" &&
            labelerService.TryGetProperty("endpoint", out JsonElement endpoint) &&
            endpoint.ValueKind == JsonValueKind.String &&
            Uri.TryCreate(endpoint.GetString(), UriKind.Absolute, out Uri? serviceUri) &&
            serviceUri is not null &&
            serviceUri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(serviceUri.UserInfo) &&
            serviceUri.AbsolutePath is "" or "/" &&
            string.IsNullOrEmpty(serviceUri.Query) &&
            string.IsNullOrEmpty(serviceUri.Fragment);

        if (!validOperation)
        {
            throw new ArgumentException("A signed PLC labeler identity operation is required.", nameof(signedOperation));
        }

        AccessCredentials credentials = Credentials ?? throw new AuthenticationRequiredException();
        JsonObject submitRequest = new()
        {
            ["operation"] = JsonNode.Parse(signedOperation.GetRawText())
        };
        AtProtoHttpClient<EmptyResponse> submitClient = new(LoggerFactory)
        {
            MaximumResponseSize = MaximumResponseSize
        };
        return await submitClient.Post<JsonObject>(
            service: Service,
            endpoint: "/xrpc/com.atproto.identity.submitPlcOperation",
            record: submitRequest,
            credentials: credentials,
            httpClient: HttpClient,
            jsonSerializerOptions: AtProtoJsonSerializerOptions.Default,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds or updates the label verification key and service in the authenticated account's PLC DID document.
    /// </summary>
    /// <param name="signingKey">The local label signing key whose public key is to be published.</param>
    /// <param name="labelerEndpoint">The HTTPS origin for the labeler service.</param>
    /// <param name="authorizationToken">An optional code obtained through <see cref="RequestPlcOperationSignature"/>.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The workflow outcome, including the signed operation when it must be submitted or retried.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signingKey"/> or <paramref name="labelerEndpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The labeler endpoint is not a valid HTTPS origin.</exception>
    /// <exception cref="AuthenticationRequiredException">The current session is not authenticated.</exception>
    /// <exception cref="InvalidOperationException">The authenticated account does not use a PLC DID.</exception>
    /// <exception cref="InvalidDataException">The current DID document cannot be safely converted to a PLC operation.</exception>
    /// <remarks>
    /// <para>The PDS signs the PLC operation using the account's authorized rotation key. The repository signing key and label signing key do not authorize DID changes.</para>
    /// <para>Existing verification methods and services are retained. A signed operation is returned after signing so an operator can recover if submission or subsequent verification fails.</para>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The request and response use JsonObject metadata from the AT Protocol source-generation context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "The request and response use JsonObject metadata from the AT Protocol source-generation context.")]
    public async Task<PlcLabelerIdentityUpdateResult> UpdatePlcLabelerIdentity(
        LabelSigningKey signingKey,
        Uri labelerEndpoint,
        string? authorizationToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        ValidateLabelerEndpoint(labelerEndpoint);

        AccessCredentials credentials = Credentials ?? throw new AuthenticationRequiredException();
        Did did = credentials.Did;
        if (!did.ToString().StartsWith("did:plc:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The authenticated account must use a did:plc identifier.");
        }

        DidDocument? currentDocument = await ResolveDidDocument(did, cancellationToken).ConfigureAwait(false);
        if (currentDocument is null)
        {
            return new(
                PlcLabelerIdentityUpdateStage.SigningFailed,
                did,
                signedOperation: null,
                didDocument: null,
                statusCode: null,
                error: null,
                recoveryAction: "Resolve the current DID document, then retry the PLC identity update.");
        }

        if (currentDocument.Id != did)
        {
            throw new InvalidDataException("The resolved DID document belongs to a different identifier.");
        }

        if (IsLabelerPublication(currentDocument, did, signingKey, labelerEndpoint))
        {
            return new(
                PlcLabelerIdentityUpdateStage.Verified,
                did,
                signedOperation: null,
                currentDocument,
                statusCode: HttpStatusCode.OK,
                error: null,
                recoveryAction: null);
        }

        JsonObject verificationMethods = CreateVerificationMethods(currentDocument, did);
        verificationMethods["atproto_label"] = $"did:key:{signingKey.ExportPublicKeyMultibase()}";

        JsonObject services = CreateServices(currentDocument, did);
        services["atproto_labeler"] = new JsonObject
        {
            ["type"] = "AtprotoLabeler",
            ["endpoint"] = labelerEndpoint.GetLeftPart(UriPartial.Authority)
        };

        JsonObject signRequest = new()
        {
            ["verificationMethods"] = verificationMethods,
            ["services"] = services
        };
        if (!string.IsNullOrWhiteSpace(authorizationToken))
        {
            signRequest["token"] = authorizationToken;
        }

        AtProtoHttpClient<JsonObject> signingClient = new(LoggerFactory)
        {
            MaximumResponseSize = MaximumResponseSize
        };
        AtProtoHttpResult<JsonObject> signResult = await signingClient.Post<JsonObject>(
            service: Service,
            endpoint: "/xrpc/com.atproto.identity.signPlcOperation",
            record: signRequest,
            credentials: credentials,
            httpClient: HttpClient,
            jsonSerializerOptions: AtProtoJsonSerializerOptions.Default,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!signResult.Succeeded)
        {
            return new(
                PlcLabelerIdentityUpdateStage.SigningFailed,
                did,
                signedOperation: null,
                didDocument: null,
                statusCode: signResult.StatusCode,
                error: signResult.AtErrorDetail,
                recoveryAction: "Resolve the PDS authorization or recovery-token requirement, then sign the operation again.");
        }

        JsonNode operationNode = signResult.Result["operation"]
            ?? throw new InvalidDataException("The PDS returned no signed PLC operation.");

        using JsonDocument operationDocument = JsonDocument.Parse(operationNode.ToJsonString());
        JsonElement signedOperation = operationDocument.RootElement.Clone();

        AtProtoHttpResult<EmptyResponse> submitResult = await SubmitPlcLabelerIdentityOperation(
            signedOperation,
            cancellationToken).ConfigureAwait(false);

        if (!submitResult.Succeeded)
        {
            return new(
                PlcLabelerIdentityUpdateStage.SubmissionFailed,
                did,
                signedOperation,
                didDocument: null,
                statusCode: submitResult.StatusCode,
                error: submitResult.AtErrorDetail,
                recoveryAction: "Resolve the DID; if the operation is not published, call SubmitPlcLabelerIdentityOperation with the returned SignedOperation.");
        }

        DidDocument? publishedDocument = await ResolveDidDocument(did, cancellationToken).ConfigureAwait(false);
        if (publishedDocument is null)
        {
            return new(
                PlcLabelerIdentityUpdateStage.VerificationFailed,
                did,
                signedOperation,
                didDocument: null,
                statusCode: HttpStatusCode.BadGateway,
                error: new AtErrorDetail("PublicationUnverified", "The PLC operation was submitted, but DID resolution did not return a document."),
                recoveryAction: "Retry DID resolution and verify #atproto_label and #atproto_labeler before retrying submission.");
        }

        try
        {
            VerifyLabelerPublication(publishedDocument, did, signingKey, labelerEndpoint);
        }
        catch (InvalidDataException exception)
        {
            return new(
                PlcLabelerIdentityUpdateStage.VerificationFailed,
                did,
                signedOperation,
                publishedDocument,
                statusCode: HttpStatusCode.BadGateway,
                error: new AtErrorDetail("PublicationUnverified", exception.Message),
                recoveryAction: "Inspect the current PLC operation and DID document before submitting another update.");
        }

        return new(
            PlcLabelerIdentityUpdateStage.Verified,
            did,
            signedOperation,
            publishedDocument,
            statusCode: HttpStatusCode.OK,
            error: null,
            recoveryAction: null);
    }

    /// <summary>Converts existing DID verification methods to the PLC service-name map without dropping keys.</summary>
    private static JsonObject CreateVerificationMethods(DidDocument didDocument, Did did)
    {
        JsonObject verificationMethods = [];
        foreach (VerificationMethod method in didDocument.VerificationMethods ?? [])
        {
            string name = GetDidDocumentFragment(method.Id, did);
            if (method.Type != "Multikey" || string.IsNullOrWhiteSpace(method.PublicKeyMultibase))
            {
                throw new InvalidDataException($"Verification method '{name}' cannot be safely represented in a PLC operation.");
            }

            if (verificationMethods.ContainsKey(name))
            {
                throw new InvalidDataException($"The DID document contains duplicate verification method '{name}'.");
            }

            verificationMethods[name] = $"did:key:{method.PublicKeyMultibase}";
        }

        return verificationMethods;
    }

    /// <summary>Converts existing DID services to the PLC service map without dropping endpoints.</summary>
    private static JsonObject CreateServices(DidDocument didDocument, Did did)
    {
        JsonObject services = [];
        foreach (DidDocService service in didDocument.Services ?? [])
        {
            string name = GetDidDocumentFragment(service.Id, did);
            if (services.ContainsKey(name))
            {
                throw new InvalidDataException($"The DID document contains duplicate service '{name}'.");
            }

            services[name] = new JsonObject
            {
                ["type"] = service.Type,
                ["endpoint"] = service.ServiceEndpoint.AbsoluteUri
            };
        }

        return services;
    }

    /// <summary>Extracts a fragment owned by the resolved DID and rejects entries with unrelated identifiers.</summary>
    private static string GetDidDocumentFragment(string identifier, Did did)
    {
        string prefix = $"{did}#";
        string name;
        if (identifier.StartsWith(prefix, StringComparison.Ordinal))
        {
            name = identifier[prefix.Length..];
        }
        else if (identifier.StartsWith('#'))
        {
            name = identifier[1..];
        }
        else
        {
            throw new InvalidDataException("A DID document entry has an unsupported identifier.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDataException("A DID document entry has an empty fragment identifier.");
        }

        return name;
    }

    /// <summary>Enforces the label lexicon's HTTPS-origin-only labeler endpoint shape.</summary>
    private static void ValidateLabelerEndpoint(Uri labelerEndpoint)
    {
        ArgumentNullException.ThrowIfNull(labelerEndpoint);
        if (!labelerEndpoint.IsAbsoluteUri ||
            labelerEndpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(labelerEndpoint.UserInfo) ||
            labelerEndpoint.AbsolutePath is not ("" or "/") ||
            !string.IsNullOrEmpty(labelerEndpoint.Query) ||
            !string.IsNullOrEmpty(labelerEndpoint.Fragment))
        {
            throw new ArgumentException(
                "The labeler endpoint must be an HTTPS origin without credentials, a path, a query, or a fragment.",
                nameof(labelerEndpoint));
        }
    }

    /// <summary>Rejects a resolved DID document that does not expose the requested key and service exactly once.</summary>
    private static void VerifyLabelerPublication(
        DidDocument document,
        Did did,
        LabelSigningKey signingKey,
        Uri endpoint)
    {
        if (!IsLabelerPublication(document, did, signingKey, endpoint))
        {
            throw new InvalidDataException("The resolved DID document does not publish the expected labeler key and endpoint.");
        }
    }

    /// <summary>Checks whether the DID document already contains the exact requested key and service.</summary>
    private static bool IsLabelerPublication(DidDocument document, Did did, LabelSigningKey signingKey, Uri endpoint)
    {
        if (document.Id != did)
        {
            return false;
        }

        string expectedKey = signingKey.ExportPublicKeyMultibase();
        VerificationMethod[] methods =
        [
            .. (document.VerificationMethods ?? [])
                .Where(method => method.Id.Equals($"#{LabelKeyFragment}", StringComparison.Ordinal) ||
                    method.Id.Equals($"{did}#{LabelKeyFragment}", StringComparison.Ordinal))
        ];
        DidDocService[] services =
        [
            .. (document.Services ?? [])
                .Where(service => service.Id.Equals($"#{LabelerServiceFragment}", StringComparison.Ordinal) ||
                    service.Id.Equals($"{did}#{LabelerServiceFragment}", StringComparison.Ordinal))
        ];
        if (methods.Length != 1 ||
            methods[0].Type != "Multikey" ||
            methods[0].Controller != did ||
            methods[0].PublicKeyMultibase != expectedKey ||
            services.Length != 1 ||
            services[0].Type != "AtprotoLabeler" ||
            services[0].ServiceEndpoint != endpoint)
        {
            return false;
        }

        return true;
    }

    private const string LabelKeyFragment = "atproto_label";
    private const string LabelerServiceFragment = "atproto_labeler";
}
