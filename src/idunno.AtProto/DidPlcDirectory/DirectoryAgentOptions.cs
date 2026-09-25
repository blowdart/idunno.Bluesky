// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.AtProto;

using Microsoft.Extensions.Logging;

namespace idunno.DidPlcDirectory;

/// <summary>
/// A class for configuring the DirectoryAgent.
/// </summary>
public sealed class DirectoryAgentOptions
{
    /// <summary>
    /// Specifies the server to use when resolving plc DIDs.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not an HTTPS URI.</exception>>
    public Uri PlcDirectoryUri
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("The PLC directory server must be an HTTPS URI.", nameof(value));
            }

            field = value;
        }
    } = DirectoryAgent.s_defaultDirectoryServer;

    /// <summary>
    /// Gets or sets any HttpClient options for the agent.
    /// </summary>
    /// <para>
    /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/>to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
    /// <see langword="false"/> if you are using a debugging proxy which does not support CRLs.
    /// </para>
    public HttpClientOptions? HttpClientOptions { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="ILoggerFactory"/>, if any, to use when creating loggers.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IMeterFactory"/>, if any, to use when creating meters.
    /// </summary>
    public IMeterFactory? MeterFactory { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of bytes read from a DID document response body. Defaults to <see cref="AtProtoHttpClient.DefaultMaximumResponseSize"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is zero or negative.</exception>
    /// <remarks>
    /// <para>
    ///   A <c>did:web</c> DID names the host its document is resolved from, so that response is untrusted and the amount read from it is limited.
    /// </para>
    /// <para>
    ///   A response larger than this fails with an <see cref="AtErrorDetail"/> whose <see cref="AtErrorDetail.Error"/> is <c>ResponseTooLarge</c>.
    /// </para>
    /// </remarks>
    public int MaximumResponseSize
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            field = value;
        }
    } = AtProtoHttpClient.DefaultMaximumResponseSize;
}