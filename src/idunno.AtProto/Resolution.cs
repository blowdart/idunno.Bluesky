// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.DidPlcDirectory;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto;

/// <summary>
/// Provides access to common resolvers used by the AtProto library.
/// </summary>
public sealed class Resolution
{
    /// <summary>
    /// As we need to use the class only as a static container, prevent instantiation.
    /// We can't use static classes as they prevent the use of CreateLogger{T}.
    /// </summary>
    private Resolution()
    {
    }

    /// <summary>
    /// Resolves a handle (domain name) to a DID.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumWellKnownResponseSize"/> is zero or negative.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<Did?> ResolveHandle(
        string handle,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWellKnownResponseSize);

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

        Logger.ResolveHandleCalled(logger, handle);

        using (HttpClientLease lease = new(httpClient, timeout))
        {
            Did? result = await AtProtoServer.ResolveHandle(
                handle,
                httpClient: lease.Client,
                loggerFactory: loggerFactory,
                maximumWellKnownResponseSize: maximumWellKnownResponseSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                Logger.CouldNotResolveHandleToDid(logger, handle);
            }
            else
            {
                Logger.ResolveHandleToDid(logger, handle, result);
            }

            return result;
        }
    }

    /// <summary>
    /// Resolves a handle (domain name) to a DID.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumWellKnownResponseSize"/> is zero or negative.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<Did?> ResolveHandle(
        Handle handle,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        return await ResolveHandle(
            handle: handle.ToString(),
            loggerFactory: loggerFactory,
            httpClient: httpClient,
            timeout: timeout,
            maximumWellKnownResponseSize: maximumWellKnownResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that <paramref name="handle"/> and <paramref name="did"/> resolve to each other.
    /// </summary>
    /// <param name="handle">The <see cref="Handle"/> to verify.</param>
    /// <param name="did">The <see cref="Did"/> the <paramref name="handle"/> is claimed to belong to.</param>
    /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If <see langword="null"/> the default directory server, https://plc.directory, will be used.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the directory response body.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>
    /// The task object representing the asynchronous operation, whose result is <see langword="true"/> if <paramref name="handle"/>
    /// and <paramref name="did"/> resolve to each other, otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> or <paramref name="did"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   A <see cref="Handle"/> and a <see cref="Did"/> are controlled by different parties, so neither on its own establishes
    ///   that they belong together. The handle owner declares a <see cref="Did"/> through DNS or <c>/.well-known/atproto-did</c>,
    ///   and the <see cref="Did"/> owner declares a handle through the <c>alsoKnownAs</c> entries of the <see cref="DidDocument"/>.
    ///   Only when both directions agree is the pairing trustworthy.
    /// </para>
    /// <para>
    ///   Verify before treating a <see cref="Handle"/> as identifying the holder of a <see cref="Did"/>. Anyone can put any handle
    ///   in a <see cref="DidDocument"/> they control, or point a handle they own at someone else's <see cref="Did"/>, so an
    ///   unverified handle proves nothing.
    /// </para>
    /// </remarks>
    public static async Task<bool> VerifyHandle(
        Handle handle,
        Did did,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(did);

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

        Logger.VerifyHandleCalled(logger, handle, did);

        DidDocument? didDocument = await ResolveDidDocument(
            did: did,
            plcDirectory: plcDirectory,
            loggerFactory: loggerFactory,
            httpClient: httpClient,
            timeout: timeout,
            maximumResponseSize: maximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (didDocument is null || !DeclaresHandle(didDocument, handle))
        {
            Logger.HandleNotDeclaredByDidDocument(logger, handle, did);
            return false;
        }

        Did? resolvedDid = await ResolveHandle(
            handle: handle,
            loggerFactory: loggerFactory,
            httpClient: httpClient,
            timeout: timeout,
            maximumWellKnownResponseSize: maximumWellKnownResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (resolvedDid is null || resolvedDid != did)
        {
            Logger.HandleDidNotResolveToDid(logger, handle, did, resolvedDid);
            return false;
        }

        Logger.HandleVerified(logger, handle, did);

        return true;
    }

    /// <summary>
    /// Resolves the <see cref="Handle"/> for the specified <paramref name="did"/>, verifying that the handle resolves back to it.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to resolve the <see cref="Handle"/> for.</param>
    /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If <see langword="null"/> the default directory server, https://plc.directory, will be used.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the directory response body.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>
    /// The task object representing the asynchronous operation, whose result is the verified <see cref="Handle"/> for the
    /// <paramref name="did"/>, or <see cref="Handle.Invalid"/> if it does not declare a handle which resolves back to it.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   The handles in a <see cref="DidDocument"/> are declared by whoever controls the <see cref="Did"/>, so a handle is only
    ///   meaningful once it has been resolved back to the <see cref="Did"/> which claims it. Each declared handle is checked in
    ///   turn and the first which resolves back to <paramref name="did"/> is returned.
    /// </para>
    /// <para>
    ///   Use <see cref="Handle.IsValid"/> on the result, or compare it to <see cref="Handle.Invalid"/>, before displaying
    ///   it or treating it as identifying the holder of <paramref name="did"/>.
    /// </para>
    /// </remarks>
    public static async Task<Handle> ResolveVerifiedHandle(
        Did did,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

        Logger.ResolveVerifiedHandleCalled(logger, did);

        DidDocument? didDocument = await ResolveDidDocument(
            did: did,
            plcDirectory: plcDirectory,
            loggerFactory: loggerFactory,
            httpClient: httpClient,
            timeout: timeout,
            maximumResponseSize: maximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (didDocument is null)
        {
            return Handle.Invalid;
        }

        foreach (Handle candidate in DeclaredHandles(didDocument))
        {
            Did? resolvedDid = await ResolveHandle(
                handle: candidate,
                loggerFactory: loggerFactory,
                httpClient: httpClient,
                timeout: timeout,
                maximumWellKnownResponseSize: maximumWellKnownResponseSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (resolvedDid is not null && resolvedDid == did)
            {
                Logger.HandleVerified(logger, candidate, did);
                return candidate;
            }

            Logger.HandleDidNotResolveToDid(logger, candidate, did, resolvedDid);
        }

        return Handle.Invalid;
    }

    /// <summary>
    /// Gets the handles declared by the <c>alsoKnownAs</c> entries of the specified <paramref name="didDocument"/>.
    /// </summary>
    /// <param name="didDocument">The <see cref="DidDocument"/> to read the declared handles from.</param>
    /// <returns>The handles declared by <paramref name="didDocument"/>.</returns>
    /// <remarks>
    /// <para>
    ///   Entries which are not <c>at://</c> URIs, or whose authority is not a valid handle, are ignored. A DID document
    ///   is supplied by whoever controls the DID, so its contents are untrusted.
    /// </para>
    /// </remarks>
    private static IEnumerable<Handle> DeclaredHandles(DidDocument didDocument)
    {
        const string atUriPrefix = "at://";

        if (didDocument.AlsoKnownAs is null)
        {
            yield break;
        }

        foreach (string alsoKnownAs in didDocument.AlsoKnownAs)
        {
            if (alsoKnownAs is null || !alsoKnownAs.StartsWith(atUriPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string candidate = alsoKnownAs[atUriPrefix.Length..];

            // An at:// URI may carry a path, query or fragment, none of which are part of the handle.
            int end = candidate.IndexOfAny(['/', '?', '#']);

            if (end >= 0)
            {
                candidate = candidate[..end];
            }

            if (Handle.TryParse(candidate, out Handle? handle) && handle is not null && handle.IsValid)
            {
                yield return handle;
            }
        }
    }

    /// <summary>
    /// Returns a flag indicating whether <paramref name="didDocument"/> declares the specified <paramref name="handle"/>.
    /// </summary>
    /// <param name="didDocument">The <see cref="DidDocument"/> to check.</param>
    /// <param name="handle">The <see cref="Handle"/> to look for.</param>
    /// <returns><see langword="true"/> if <paramref name="didDocument"/> declares <paramref name="handle"/>, otherwise <see langword="false"/>.</returns>
    private static bool DeclaresHandle(DidDocument didDocument, Handle handle)
    {
        return DeclaredHandles(didDocument).Contains(handle);
    }

    /// <summary>
    /// Resolves the <see cref="DidDocument"/> for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to resolve the <see cref="DidDocument"/> for.</param>
    /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If <see langword="null"/> the default directory server, https://plc.directory, will be used.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the directory response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<DidDocument?> ResolveDidDocument(
        Did did,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        plcDirectory ??= DirectoryAgent.s_defaultDirectoryServer;

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

        Logger.ResolveDidDocumentCalled(logger, did);

        DidDocument? didDocument = null;

        using (HttpClientLease lease = new(httpClient, timeout))
        {
            AtProtoHttpResult<DidDocument> didDocumentResolutionResult = await
                DirectoryServer.ResolveDidDocument(
                    did: did,
                    directory: plcDirectory,
                    httpClient: lease.Client,
                    loggerFactory: loggerFactory,
                    maximumResponseSize: maximumResponseSize,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (didDocumentResolutionResult.Succeeded)
            {
                didDocument = didDocumentResolutionResult.Result;
            }
            else
            {
                Logger.ResolveDidDocumentFailed(logger, did, didDocumentResolutionResult.StatusCode);
            }
        }

        return didDocument;
    }

    /// <summary>
    /// Resolves the <see cref="DidDocument"/> for the specified <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="Handle"/> to resolve the <see cref="DidDocument"/> for.</param>
    /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If <see langword="null"/> the default directory server, https://plc.directory, will be used.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the directory response body.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response when resolving the handle.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> could not be resolved to a <see cref="Did"/>.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<DidDocument?> ResolveDidDocument(
        Handle handle,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        plcDirectory ??= DirectoryAgent.s_defaultDirectoryServer;

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

        Did? did = await ResolveHandle(
            handle: handle,
            loggerFactory: loggerFactory,
            httpClient: httpClient,
            timeout: timeout,
            maximumWellKnownResponseSize: maximumWellKnownResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException("Could not resolve handle to DID.", nameof(handle));

        Logger.ResolveDidDocumentCalled(logger, did);

        DidDocument? didDocument = null;

        using (HttpClientLease lease = new(httpClient, timeout))
        {
            AtProtoHttpResult<DidDocument> didDocumentResolutionResult = await
                DirectoryServer.ResolveDidDocument(
                    did: did,
                    directory: plcDirectory,
                    httpClient: lease.Client,
                    loggerFactory: loggerFactory,
                    maximumResponseSize: maximumResponseSize,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (didDocumentResolutionResult.Succeeded)
            {
                didDocument = didDocumentResolutionResult.Result;
            }
            else
            {
                Logger.ResolveDidDocumentFailed(logger, did, didDocumentResolutionResult.StatusCode);
            }
        }

        return didDocument;
    }

    /// <summary>
    /// Resolves the <see cref="DidDocument"/> for the specified <paramref name="atIdentifier"/>.
    /// </summary>
    /// <param name="atIdentifier">The AtIdentifier to resolve the <see cref="DidDocument"/> for.</param>
    /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If <see langword="null"/> the default directory server, https://plc.directory, will be used.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the directory response body.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response when resolving a handle.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atIdentifier"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atIdentifier"/> could not be resolved to a <see cref="Did"/> or a <see cref="Handle"/>.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<DidDocument?> ResolveDidDocument(
        string atIdentifier,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(atIdentifier);

        if (AtIdentifier.TryParse(atIdentifier, out AtIdentifier? parsedAtIdentifier) && parsedAtIdentifier is not null)
        {
            if (parsedAtIdentifier is Did did)
            {
                return await ResolveDidDocument(
                    did: did,
                    plcDirectory: plcDirectory,
                    loggerFactory: loggerFactory,
                    httpClient: httpClient,
                    timeout: timeout,
                    maximumResponseSize: maximumResponseSize,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else if (parsedAtIdentifier is Handle handle)
            {
                return await ResolveDidDocument(
                    handle: handle,
                    plcDirectory: plcDirectory,
                    loggerFactory: loggerFactory,
                    httpClient: httpClient,
                    timeout: timeout,
                    maximumResponseSize: maximumResponseSize,
                    maximumWellKnownResponseSize: maximumWellKnownResponseSize,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException("AtIdentifier is of an unknown type.", nameof(atIdentifier));
            }
        }
        else
        {
            throw new ArgumentException("Could not parse AtIdentifier.", nameof(atIdentifier));
        }
    }

    /// <summary>
    /// Resolves the Personal Data Server (PDS) <see cref="Uri"/>for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to resolve the PDS <see cref="Uri"/> for.</param>
    /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If <see langword="null"/> the default directory server, https://plc.directory, will be used.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the directory response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<Uri?> ResolvePds(
        Did did,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

        Logger.ResolvePdsCalled(logger, did);

        Uri? pds = null;

        DidDocument? didDocument = await ResolveDidDocument(
            did,
            plcDirectory: plcDirectory,
            loggerFactory: loggerFactory,
            httpClient: httpClient,
            timeout: timeout,
            maximumResponseSize: maximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (didDocument is not null && didDocument.Services is not null)
        {
            pds = didDocument.Services.FirstOrDefault(s => s.Id == @"#atproto_pds")?.ServiceEndpoint;

            if (pds is not null && !IsSupportedServiceEndpoint(pds))
            {
                Logger.UnsupportedPdsUri(logger, did, pds);
                pds = null;
            }
        }

        if (pds is null)
        {
            Logger.ResolvePdsFailed(logger, did);
        }

        return pds;
    }

    /// <summary>
    /// Resolves the Personal Data Server (PDS) <see cref="Uri"/>for the specified <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="Handle"/> to resolve the PDS <see cref="Uri"/> for.</param>
    /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If <see langword="null"/> the default directory server, https://plc.directory, will be used.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the directory response body.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response when resolving the handle.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> could not be resolved to a <see cref="Did"/>.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<Uri?> ResolvePds(
        Handle handle,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        Did? did = await ResolveHandle(
            handle: handle,
            loggerFactory: loggerFactory,
            httpClient: httpClient,
            timeout: timeout,
            maximumWellKnownResponseSize: maximumWellKnownResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException("Could not resolve handle to DID.", nameof(handle));

        return await ResolvePds(
            did,
            plcDirectory: plcDirectory,
            loggerFactory: loggerFactory,
            httpClient: httpClient,
            timeout: timeout,
            maximumResponseSize: maximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the Personal Data Server (PDS) <see cref="Uri"/>for the specified <paramref name="atIdentifier"/>.
    /// </summary>
    /// <param name="atIdentifier">The <see cref="Handle"/> to resolve the PDS <see cref="Uri"/> for.</param>
    /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If <see langword="null"/> the default directory server, https://plc.directory, will be used.</param>
    /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
    /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is <see langword="null"/>.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the directory response body.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response when resolving a handle.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atIdentifier"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atIdentifier"/> could not be resolved to a <see cref="Did"/> or <see cref="Handle"/>.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<Uri?> ResolvePds(
        string atIdentifier,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(atIdentifier);

        if (AtIdentifier.TryParse(atIdentifier, out AtIdentifier? parsedAtIdentifier) && parsedAtIdentifier is not null)
        {
            if (parsedAtIdentifier is Did did)
            {
                return await ResolvePds(
                    did: did,
                    plcDirectory: plcDirectory,
                    loggerFactory: loggerFactory,
                    httpClient: httpClient,
                    timeout: timeout,
                    maximumResponseSize: maximumResponseSize,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else if (parsedAtIdentifier is Handle handle)
            {
                return await ResolvePds(
                    handle: handle,
                    plcDirectory: plcDirectory,
                    loggerFactory: loggerFactory,
                    httpClient: httpClient,
                    timeout: timeout,
                    maximumResponseSize: maximumResponseSize,
                    maximumWellKnownResponseSize: maximumWellKnownResponseSize,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException("AtIdentifier is of an unknown type.", nameof(atIdentifier));
            }
        }
        else
        {
            throw new ArgumentException("Could not parse AtIdentifier.", nameof(atIdentifier));
        }
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="serviceEndpoint"/> is a service endpoint this library will issue requests to.
    /// </summary>
    /// <param name="serviceEndpoint">The service endpoint to check.</param>
    /// <returns><see langword="true"/> if <paramref name="serviceEndpoint"/> is supported, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    ///   A DID document is served by whoever controls the DID, so the service endpoints it carries are chosen by
    ///   a third party rather than by the calling application. Requests to a PDS carry access credentials, so an
    ///   endpoint which would send those credentials in clear text is rejected rather than used.
    /// </para>
    /// </remarks>
    internal static bool IsSupportedServiceEndpoint(Uri serviceEndpoint)
    {
        if (serviceEndpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // http is only supported against loopback, for testing and development, where a trusted certificate may not exist.
        return serviceEndpoint.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && serviceEndpoint.IsLoopback;
    }
}