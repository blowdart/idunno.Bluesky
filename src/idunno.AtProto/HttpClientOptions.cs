// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// Encapsulates configuration options for the http client used to make HTTP requests.
    /// </summary>
    public sealed class HttpClientOptions
    {
        /// <summary>
        /// Gets or sets the user agent string to use, if any.
        /// </summary>
        public string? HttpUserAgent { get; set; }

        /// <summary>
        /// Gets or sets the proxy URI to use, if any.
        /// </summary>
        public Uri? ProxyUri { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether certificate revocation lists should be checked. Defaults to <see langword="true" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting this to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        /// </remarks>
        public bool CheckCertificateRevocationList { get; set; } = true;

        /// <summary>
        /// Overrides the default amount of time to wait before an HTTP request times out.
        /// </summary>
        public TimeSpan? Timeout { get; set; }
    }
}
