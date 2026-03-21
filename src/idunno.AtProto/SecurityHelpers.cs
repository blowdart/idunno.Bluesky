// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetTools;

namespace idunno.AtProto;

/// <summary>
/// Contains helper methods for validating URIs and IP addresses to mitigate SSRF (Server-Side Request Forgery) vulnerabilities.
/// </summary>
public sealed class SecurityHelpers
{
    private SecurityHelpers()
    {
    }

    /// <summary>
    /// Builds a <see cref="SocketsHttpHandler"/> with SSRF protections implemented in the
    /// <see cref="SocketsHttpHandler.ConnectCallback"/>. The handler will attempt to resolve the target host to an IP address and validate that each resolved address
    /// is not considered unsafe before allowing a connection to be established.
    /// </summary>
    /// <param name="connectTimeout">The connect timeout, in seconds.</param>
    /// <param name="proxyUri">An optional proxy <see cref="Uri"/>.</param>
    /// <param name="checkCertificateRevocationList">Flag indicating whether to check the certificate revocation list. Setting this to <see langword="true"/> can introduce security vulnerabilities and should only be enabled if necessary.</param>
    /// <param name="allowAutoRedirect">Flag indicating whether to allow auto-redirects. Setting this to <see langword="true"/> can introduce security vulnerabilities and should only be enabled if necessary.</param>
    /// <returns>A <see cref="SocketsHttpHandler"/> with SSRF protections.</returns>
    public static SocketsHttpHandler BuildSSRFHttpHandler(
        TimeSpan? connectTimeout = null,
        Uri? proxyUri = null,
        bool checkCertificateRevocationList = true,
        bool allowAutoRedirect = false)
    {
        connectTimeout ??= TimeSpan.FromSeconds(30);

        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = allowAutoRedirect,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = connectTimeout.Value,
            EnableMultipleHttp2Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                CertificateRevocationCheckMode = checkCertificateRevocationList
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck
            },
            UseCookies = false,

            ConnectCallback = static async (context, cancellationToken) =>
            {
                ArgumentNullException.ThrowIfNull(context);

                // Do not cache results of DNS resolution to ensure that SSRF protections are applied to each connection attempt, even if the same host is targeted multiple times.
                // This may result in additional latency for connections due to DNS lookups, but is necessary as caching would introduce a TOCTOU (Time of Check to Time of Use)
                // vulnerability where an attacker could change the resolved IP address after validation but before connection.

                IPAddress[] addresses;
                List<IPAddress> safeIPAddresses = [];

                if (IPAddress.TryParse(context.DnsEndPoint.Host, out IPAddress? parsedAddress))
                {
                    addresses = [parsedAddress];
                }
                else
                {
                    IPHostEntry entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
                    addresses = entry.AddressList;
                }
                safeIPAddresses.AddRange(from IPAddress address in addresses
                                         where !IsUnsafeIpAddress(address)
                                         select address);

                if (safeIPAddresses.Count > 0)
                {
                    // Attempt to connect to each safe IP address until a successful connection is made.

                    foreach (IPAddress address in safeIPAddresses)
                    {
                        Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                        }
                        catch (SocketException)
                        {
                            socket.Dispose();
                            continue;
                        }

                        return new NetworkStream(socket, ownsSocket: true);
                    }

                    throw new SocketException((int)SocketError.HostUnreachable);    
                }

                throw new HttpRequestException($"Connection to {context.DnsEndPoint.Host} was blocked by SSRF protection. All resolved addresses are unsafe.");
            }
        };

        if (proxyUri is not null)
        {
            handler.Proxy = new WebProxy(proxyUri);
            handler.UseProxy = true;
        }

        return handler;
    }

    /// <summary>
    /// Implements simple SSRF validation on the specified <paramref name="uri"/> by checking if the host resolves to a public IP address.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> to validate.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging any validation issues.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns><see langword="true" /> if the <paramref name="uri" /> is considered safe, otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    public static async Task<bool> DefaultDiscoveryUriValidator(Uri uri, ILoggerFactory? loggerFactory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        loggerFactory ??= NullLoggerFactory.Instance;

        ILogger<SecurityHelpers> logger = loggerFactory.CreateLogger<SecurityHelpers>();

        if (IsUnsafeUri(uri, logger))
        {
            return false;
        }

        if (uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
        {
            var ipAddress = IPAddress.Parse(uri.Host);

            if (IsUnsafeIpAddress(ipAddress))
            {
                Logger.UnsafeUriHostIpAddress(logger, uri, ipAddress);
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

            foreach (IPAddress entry in hostEntry.AddressList.Where(IsUnsafeIpAddress))
            {
                Logger.UnsafeUriHostIpAddress(logger, uri, entry);
                discoveredUnsafeIPAddress = true;
            }

            return !discoveredUnsafeIPAddress;
        }
    }

    internal static bool IsUnsafeUri(Uri uri, ILogger logger)
    {
        if (uri.HostNameType != UriHostNameType.Dns &&
            uri.HostNameType != UriHostNameType.IPv4 &&
            uri.HostNameType != UriHostNameType.IPv6)
        {
            Logger.UnknownHostType(logger, uri);
            return true;
        }

        if (!uri.IsAbsoluteUri ||
            uri.IsLoopback ||
            uri.IsUnc)
        {
            Logger.UriNotAbsoluteOrLoopback(logger, uri);
            return true;
        }

        if (!Uri.UriSchemeHttps.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase) &&
            !Uri.UriSchemeHttp.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            Logger.UnsafeUriScheme(logger, uri);
            return true;
        }
        else
        {
            return false;
        }
    }

    // IPv4 private address ranges https://datatracker.ietf.org/doc/html/rfc1918
    private static readonly IPAddressRange s_ipv4Private10_8 = IPAddressRange.Parse("10.0.0.0/8");
    private static readonly IPAddressRange s_ipv4Private172_16_12 = IPAddressRange.Parse("172.16.0.0/12");
    private static readonly IPAddressRange s_ipv4Private192_168_16 = IPAddressRange.Parse("192.168.0.0/16");

    // IPv4 loopback https://datatracker.ietf.org/doc/html/rfc1122
    private static readonly IPAddressRange s_ipv4Loopback127_8 = IPAddressRange.Parse("127.0.0.0/8");

    // IPv4 link-local https://datatracker.ietf.org/doc/html/rfc3927
    private static readonly IPAddressRange s_ipv4LinkLocal169_254_16 = IPAddressRange.Parse("169.254.0.0/16");

    // IPv4 carrier-grade NAT https://datatracker.ietf.org/doc/html/rfc6598
    private static readonly IPAddressRange s_ipv4Cgnat100_64_10 = IPAddressRange.Parse("100.64.0.0/10");

    // IPv4 "this network" https://datatracker.ietf.org/doc/html/rfc1122
    private static readonly IPAddressRange s_ipv4ThisNetwork0_8 = IPAddressRange.Parse("0.0.0.0/8");

    // IPv4 benchmarking https://datatracker.ietf.org/doc/html/rfc2544
    private static readonly IPAddressRange s_ipv4Benchmark198_18_15 = IPAddressRange.Parse("198.18.0.0/15");

    // IPv4 documentation/test ranges https://datatracker.ietf.org/doc/html/rfc5737
    private static readonly IPAddressRange s_ipv4TestNet192_0_2_24 = IPAddressRange.Parse("192.0.2.0/24");
    private static readonly IPAddressRange s_ipv4TestNet198_51_100_24 = IPAddressRange.Parse("198.51.100.0/24");
    private static readonly IPAddressRange s_ipv4TestNet203_0_113_24 = IPAddressRange.Parse("203.0.113.0/24");

    // IPv4 IETF protocol assignments https://datatracker.ietf.org/doc/html/rfc6890
    private static readonly IPAddressRange s_ipv4IetfProtocolAssignments192_0_0_24 = IPAddressRange.Parse("192.0.0.0/24");

    // IPv4 multicast https://datatracker.ietf.org/doc/html/rfc1112
    private static readonly IPAddressRange s_ipv4Multicast224_4 = IPAddressRange.Parse("224.0.0.0/4");

    // IPv4 reserved https://datatracker.ietf.org/doc/html/rfc1112
    private static readonly IPAddressRange s_ipv4Reserved240_4 = IPAddressRange.Parse("240.0.0.0/4");

    // IPv4 limited broadcast
    private static readonly IPAddress s_ipv4Broadcast = IPAddress.Parse("255.255.255.255");

    // Cloud metadata endpoint used by AWS, Azure, and Google Cloud.
    private static readonly IPAddress s_cloudMetaDataEndpoint = IPAddress.Parse("169.254.169.254");

    // IPv6 unique local https://datatracker.ietf.org/doc/html/rfc4193
    private static readonly IPAddressRange s_ipv6UniqueLocalFd00_8 = IPAddressRange.Parse("fd00::/8");

    // IPv6 documentation range https://datatracker.ietf.org/doc/html/rfc3849
    private static readonly IPAddressRange s_ipv6Documentation2001Db8_32 = IPAddressRange.Parse("2001:db8::/32");

    internal static bool IsUnsafeIpAddress(IPAddress ipAddress)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);

        // Normalize IPv4-mapped IPv6 addresses (e.g. ::ffff:127.0.0.1) to IPv4 before range checks.
        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        // Block unspecified addresses (IPv4 0.0.0.0 and IPv6 ::).
        if (ipAddress.Equals(IPAddress.Any) || ipAddress.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        // Block loopback: IPv4 127/8 and IPv6 ::1.
        if (IPAddress.IsLoopback(ipAddress))
        {
            return true;
        }

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // Explicit cloud metadata SSRF target.
            if (ipAddress.Equals(s_cloudMetaDataEndpoint))
            {
                return true;
            }

            // Limited broadcast.
            if (ipAddress.Equals(s_ipv4Broadcast))
            {
                return true;
            }

            return
                s_ipv4ThisNetwork0_8.Contains(ipAddress) ||
                s_ipv4Loopback127_8.Contains(ipAddress) ||
                s_ipv4Private10_8.Contains(ipAddress) ||
                s_ipv4Private172_16_12.Contains(ipAddress) ||
                s_ipv4Private192_168_16.Contains(ipAddress) ||
                s_ipv4Cgnat100_64_10.Contains(ipAddress) ||
                s_ipv4LinkLocal169_254_16.Contains(ipAddress) ||
                s_ipv4Benchmark198_18_15.Contains(ipAddress) ||
                s_ipv4TestNet192_0_2_24.Contains(ipAddress) ||
                s_ipv4TestNet198_51_100_24.Contains(ipAddress) ||
                s_ipv4TestNet203_0_113_24.Contains(ipAddress) ||
                s_ipv4IetfProtocolAssignments192_0_0_24.Contains(ipAddress) ||
                s_ipv4Multicast224_4.Contains(ipAddress) ||
                s_ipv4Reserved240_4.Contains(ipAddress);
        }

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return
                ipAddress.IsIPv6Multicast ||
                ipAddress.IsIPv6LinkLocal ||
                ipAddress.IsIPv6SiteLocal ||
                ipAddress.IsIPv6UniqueLocal ||
                s_ipv6UniqueLocalFd00_8.Contains(ipAddress) ||
                s_ipv6Documentation2001Db8_32.Contains(ipAddress) ||
                ipAddress.Equals(IPAddress.IPv6Loopback);
        }

        // Unknown address family: fail closed.
        return true;
    }

}
