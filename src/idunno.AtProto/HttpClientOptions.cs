// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// Encapsulates configuration options for the http client used to make HTTP requests.
    /// </summary>
    /// <param name="httpUserAgent">The user agent string to use when making requests.</param>
    /// <param name="proxyUri">The <see cref="Uri"/> of any proxy to use when making requests.</param>
    /// <param name="checkCertificateRevocationList">Flag indicating whether certificate revocation checks should be made.</param>
    /// <param name="timeout">The <see cref="TimeSpan"/> after which requests should time out.</param>
    /// <remarks>
    /// <para>
    /// Setting <paramref name="checkCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
    /// false if you are using a debugging proxy which does not support CRLs.
    /// </para>
    /// </remarks>
    public sealed class HttpClientOptions(string? httpUserAgent = null, Uri? proxyUri = null, bool checkCertificateRevocationList = true, TimeSpan? timeout = null)
    {

        /// <summary>
        /// Gets or sets the user agent string to use, if any.
        /// </summary>
        public string? HttpUserAgent { get; set; } = httpUserAgent;

        /// <summary>
        /// Gets or sets the proxy URI to use, if any.
        /// </summary>
        public Uri? ProxyUri { get; set; } = proxyUri;

        /// <summary>
        /// Gets or sets a flag indicating whether certificate revocation lists should be checked. Defaults to <see langword="true" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting this to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        /// </remarks>
        public bool CheckCertificateRevocationList { get; set; } = checkCertificateRevocationList;

        /// <summary>
        /// Overrides the default amount of time to wait before an HTTP request times out.
        /// </summary>
        public TimeSpan? Timeout { get; set; } = timeout;
    }
}
