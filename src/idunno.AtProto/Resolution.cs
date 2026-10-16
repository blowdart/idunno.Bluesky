// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

/// <summary>
/// Provides access to common resolvers used by the AtProto library.
/// </summary>
/// <remarks>
/// <para>This class is obsolete. Use <see cref="IdentityResolution"/> instead.</para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1133", Justification = "Retained for source compatibility with v7 consumers.")]
[Obsolete("Use IdentityResolution instead.")]
public sealed class Resolution
{
    private Resolution()
    {
    }

    /// <inheritdoc cref="IdentityResolution.ResolveHandleAsync(string, ILoggerFactory?, HttpClient?, TimeSpan?, int, CancellationToken)"/>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static Task<Did?> ResolveHandle(
        string handle,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolveHandleAsync(handle, loggerFactory, httpClient, timeout, maximumWellKnownResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.ResolveHandleAsync(Handle, ILoggerFactory?, HttpClient?, TimeSpan?, int, CancellationToken)"/>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static Task<Did?> ResolveHandle(
        Handle handle,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolveHandleAsync(handle, loggerFactory, httpClient, timeout, maximumWellKnownResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.VerifyHandleAsync(Handle, Did, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, int, CancellationToken)"/>
    public static Task<bool> VerifyHandle(
        Handle handle,
        Did did,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.VerifyHandleAsync(handle, did, plcDirectory, loggerFactory, httpClient, timeout, maximumResponseSize, maximumWellKnownResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.ResolveVerifiedHandleAsync(Did, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, int, CancellationToken)"/>
    public static Task<Handle> ResolveVerifiedHandle(
        Did did,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolveVerifiedHandleAsync(did, plcDirectory, loggerFactory, httpClient, timeout, maximumResponseSize, maximumWellKnownResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.ResolveDidDocumentAsync(Did, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, CancellationToken)"/>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static Task<DidDocument?> ResolveDidDocument(
        Did did,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolveDidDocumentAsync(did, plcDirectory, loggerFactory, httpClient, timeout, maximumResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.ResolveDidDocumentAsync(Handle, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, int, CancellationToken)"/>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static Task<DidDocument?> ResolveDidDocument(
        Handle handle,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolveDidDocumentAsync(handle, plcDirectory, loggerFactory, httpClient, timeout, maximumResponseSize, maximumWellKnownResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.ResolveDidDocumentAsync(string, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, int, CancellationToken)"/>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static Task<DidDocument?> ResolveDidDocument(
        string atIdentifier,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolveDidDocumentAsync(atIdentifier, plcDirectory, loggerFactory, httpClient, timeout, maximumResponseSize, maximumWellKnownResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.ResolvePdsAsync(Did, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, CancellationToken)"/>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static Task<Uri?> ResolvePds(
        Did did,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolvePdsAsync(did, plcDirectory, loggerFactory, httpClient, timeout, maximumResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.ResolvePdsAsync(Handle, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, int, CancellationToken)"/>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static Task<Uri?> ResolvePds(
        Handle handle,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolvePdsAsync(handle, plcDirectory, loggerFactory, httpClient, timeout, maximumResponseSize, maximumWellKnownResponseSize, cancellationToken);

    /// <inheritdoc cref="IdentityResolution.ResolvePdsAsync(string, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, int, CancellationToken)"/>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static Task<Uri?> ResolvePds(
        string atIdentifier,
        Uri? plcDirectory = null,
        ILoggerFactory? loggerFactory = null,
        HttpClient? httpClient = null,
        TimeSpan? timeout = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        int maximumWellKnownResponseSize = AtProtoServer.DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default) =>
        IdentityResolution.ResolvePdsAsync(atIdentifier, plcDirectory, loggerFactory, httpClient, timeout, maximumResponseSize, maximumWellKnownResponseSize, cancellationToken);
}
