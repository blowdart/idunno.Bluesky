// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.Security;

namespace idunno.AtProto;

/// <summary>
/// Contains helper methods for validating URIs and IP addresses to mitigate SSRF (Server-Side Request Forgery) vulnerabilities.
/// </summary>
internal sealed class SecurityHelpers
{
    private SecurityHelpers()
    {
    }

    /// <summary>
    /// Implements simple SSRF validation on the specified <paramref name="uri"/> by checking if the host resolves to a public IP address.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> to validate.</param>
    /// <param name="allowInsecureProtocols">Indicates whether insecure protocols (e.g., HTTP) are allowed.</param>
    /// <param name="allowLoopback">Indicates whether loopback addresses are allowed.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging any validation issues.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns><see langword="true" /> if the <paramref name="uri" /> is considered safe, otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    public static async Task<bool> DefaultDiscoveryUriValidator(
        Uri uri,
        bool allowInsecureProtocols,
        bool allowLoopback,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        loggerFactory ??= NullLoggerFactory.Instance;

        ILogger<SecurityHelpers> logger = loggerFactory.CreateLogger<SecurityHelpers>();

        if (Ssrf.IsUnsafeUri(
            uri: uri,
            allowInsecureProtocols: allowInsecureProtocols,
            allowLoopback: allowLoopback))
        {
            Logger.UnsafeUri(logger, uri);
            return false;
        }

        if (uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
        {
            var ipAddress = IPAddress.Parse(uri.Host);

            if (Ssrf.IsUnsafeIpAddress(ipAddress, allowLoopback))
            {
                Logger.UnsafeIpAddress(logger, uri, ipAddress);
                return false;
            }
            else
            {
                return true;
            }
        }
        else
        {
            IPHostEntry? hostEntry = await Dns.GetHostEntryAsync(uri.Host, cancellationToken).ConfigureAwait(false);
            if (hostEntry is null || hostEntry.AddressList is null)
            {
                Logger.UriDoesNotResolve(logger, uri);
                return false;
            }

            bool discoveredUnsafeIPAddress = false;

            foreach (IPAddress ipAddress in hostEntry.AddressList.Where(ip => Ssrf.IsUnsafeIpAddress(ip, allowLoopback)))
            {
                Logger.UnsafeIpAddress(logger, uri, ipAddress);
                discoveredUnsafeIPAddress = true;
            }

            return !discoveredUnsafeIPAddress;
        }
    }
}
