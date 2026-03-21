// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using NetTools;

namespace idunno.AtProto;

/// <summary>
/// Contains helper methods for validating URIs and IP addresses to mitigate SSRF (Server-Side Request Forgery) vulnerabilities.
/// </summary>
public static class SecurityHelpers
{
    /// <summary>
    /// Implements simple SSRF validation on the specified <paramref name="uri"/> by checking if the host resolves to a public IP address.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> to validate.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns><see langword="true" /> if the <paramref name="uri" /> is considered safe, otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    public static async Task<bool> DefaultDiscoveryUriValidator(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (IsUnsafeUri(uri))
        {
            return false;
        }

        if (uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
        {
            return !IsUnsafeIpAddress(IPAddress.Parse(uri.Host));
        }
        else
        {
            IPHostEntry? hostEntry = await Dns.GetHostEntryAsync(uri.Host, cancellationToken).ConfigureAwait(false);
            if (hostEntry is null || hostEntry.AddressList is null)
            {
                return false;
            }

            return !hostEntry.AddressList.Any(IsUnsafeIpAddress);
        }
    }

    internal static bool IsUnsafeUri(Uri uri)
    {
        if (uri.HostNameType != UriHostNameType.Dns &&
            uri.HostNameType != UriHostNameType.IPv4 &&
            uri.HostNameType != UriHostNameType.IPv6)

        {
            return true;
        }

        if (!uri.IsAbsoluteUri ||
            uri.IsLoopback ||
            uri.IsUnc)
        {
            return true;
        }

        return
            !Uri.UriSchemeHttps.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase) &&
            !Uri.UriSchemeHttp.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase);
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
