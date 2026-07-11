// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Net.Http.Headers;

using idunno.AtProto;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

/// <summary>
/// A helper class to perform HTTP requests against an Bluesky APIs.
/// This class only differs from <see cref="AtProtoHttpClient{TResult}"/> by additionally mapping Bluesky specific API errors to more specific error types.
/// </summary>
/// <typeparam name="TResult">The type of class to use when deserializing results from an Bluesky API call.</typeparam>
/// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
/// <param name="requestHeaders">Optional headers to add to the requests this instance makes.</param>
/// <param name="loggerFactory">An optional logger factory to create loggers from.</param>
/// <param name="meterFactory">An optional meter factory to create meters from.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/></para>
/// </remarks>
public class BlueskyHttpClient<TResult>(
    string? serviceProxy,
    ICollection<NameValueHeaderValue>? requestHeaders,
    ILoggerFactory? loggerFactory,
    IMeterFactory? meterFactory) : AtProtoHttpClient<TResult>(
        serviceProxy: serviceProxy,
        requestHeaders: requestHeaders,
        loggerFactory: loggerFactory,
        meterFactory: meterFactory,
        errorMappers: s_blueskyChainedErrorMappers) where TResult : class
{
    private static readonly ReadOnlyCollection<Func<AtErrorDetail?, AtErrorDetail?>> s_blueskyChainedErrorMappers =
        new(list: [BlueskyError.Map, AtProtoError.Map]);

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    public BlueskyHttpClient()
        : this(
              serviceProxy: null,
              requestHeaders: null,
              loggerFactory: null,
              meterFactory: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
    public BlueskyHttpClient(ILoggerFactory? loggerFactory)
        : this(
            serviceProxy: null,
            requestHeaders: null,
            loggerFactory: loggerFactory,
            meterFactory: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="serviceProxy"/> is <see langword="null"/> or white space.</exception>
    /// <remarks>
    ///<para>Passing <see langword="null"/> as the <paramref name="serviceProxy"/> value will suppress the checks for the presence of the atproto-proxy header on requests by this instance.</para>
    /// </remarks>
    public BlueskyHttpClient(string? serviceProxy)
        : this(
            serviceProxy: serviceProxy,
            requestHeaders: null,
            loggerFactory: null,
            meterFactory: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
    /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="serviceProxy"/> is <see langword="null"/> or white space.</exception>
    /// <remarks>
    ///<para>Passing <see langword="null"/> as the <paramref name="serviceProxy"/> value will suppress the checks for the presence of the atproto-proxy header on requests by this instance.</para>
    /// </remarks>
    public BlueskyHttpClient(string? serviceProxy, ILoggerFactory? loggerFactory) :
        this(
            serviceProxy: serviceProxy,
            requestHeaders: null,
            loggerFactory: loggerFactory,
            meterFactory: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
    /// <param name="requestHeader">An header to add to the requests this instance makes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestHeader"/> is <see langword="null"/>.</exception>
    public BlueskyHttpClient(string serviceProxy, NameValueHeaderValue requestHeader)
        : this(
            serviceProxy: serviceProxy,
            requestHeaders: [requestHeader],
            loggerFactory: null,
            meterFactory: null)
    {
        ArgumentNullException.ThrowIfNull(requestHeader);
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
    /// <param name="requestHeader">An header to add to the requests this instance makes.</param>
    /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestHeader"/> is <see langword="null"/>.</exception>
    public BlueskyHttpClient(
        string serviceProxy,
        NameValueHeaderValue requestHeader,
        ILoggerFactory? loggerFactory)
        : this(
            serviceProxy: serviceProxy,
            requestHeaders: [requestHeader],
            loggerFactory: loggerFactory,
            meterFactory: null)
    {
        ArgumentNullException.ThrowIfNull(requestHeader);
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
    /// <param name="requestHeader">An header to add to the requests this instance makes.</param>
    /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
    /// <param name="meterFactory">An optional meter factory to create meters from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestHeader"/> is <see langword="null"/>.</exception>
    public BlueskyHttpClient(
        string serviceProxy,
        NameValueHeaderValue requestHeader,
        ILoggerFactory? loggerFactory,
        IMeterFactory? meterFactory) : this(
            serviceProxy: serviceProxy,
            requestHeaders: [requestHeader],
            loggerFactory: loggerFactory,
            meterFactory: meterFactory)
    {
        ArgumentNullException.ThrowIfNull(requestHeader);
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
    /// <param name="requestHeaders">Optional headers to add to the requests this instance makes.</param>
    public BlueskyHttpClient(
        string serviceProxy,
        ICollection<NameValueHeaderValue>? requestHeaders)
        : this(
            serviceProxy: serviceProxy,
            requestHeaders: requestHeaders,
            loggerFactory: null,
            meterFactory: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
    /// <param name="requestHeaders">Optional headers to add to the requests this instance makes.</param>
    /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
    public BlueskyHttpClient(
        string serviceProxy,
        ICollection<NameValueHeaderValue>? requestHeaders,
        ILoggerFactory? loggerFactory)
        : this(
            serviceProxy: serviceProxy,
            requestHeaders: requestHeaders,
            loggerFactory: loggerFactory,
            meterFactory: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyHttpClient{TResult}"/>
    /// </summary>
    /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
    /// <param name="loggerFactory">An optional logger factory to create loggers from.</param>
    /// <param name="meterFactory">An optional meter factory to create meters from.</param>
    public BlueskyHttpClient(
        string? serviceProxy,
        ILoggerFactory? loggerFactory,
        IMeterFactory? meterFactory)
        : this(
            serviceProxy: serviceProxy,
            requestHeaders: null,
            loggerFactory: loggerFactory,
            meterFactory: meterFactory)
    {
    }
}