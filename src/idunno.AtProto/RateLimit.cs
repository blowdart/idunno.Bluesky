// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;

namespace idunno.AtProto
{
    /// <summary>
    /// Rate limit information returned by an API call.
    /// </summary>
    /// <remarks>
    /// <para>Further information can be the <see href="https://docs.bsky.app/docs/advanced-guides/rate-limits">Bluesky Rate Limit documentation</see>.</para>
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record RateLimit
    {
        internal RateLimit(int limit, int remaining, long reset, string policy)
        {
            Limit = limit;
            Remaining = remaining;
            Reset = DateTimeOffset.FromUnixTimeSeconds(reset);

            if (policy is not null && policy.Contains(";w=", StringComparison.Ordinal))
            {
                Policy = new RateLimitPolicy(policy);
            }
        }

        internal RateLimit(int limit, int remaining, long reset, int readPolicy, int writePolicy)
        {
            Limit = limit;
            Remaining = remaining;
            Reset = DateTimeOffset.FromUnixTimeSeconds(reset);
            Policy = new RateLimitPolicy(readPolicy, writePolicy);
        }

        /// <summary>
        /// Gets the maximum number of API calls that can be made before waiting for the limit to reset.
        /// </summary>
        public int Limit { get; init; }

        /// <summary>
        /// Gets the number of remaining API calls that can be made before waiting for the limit to reset.
        /// </summary>
        public int Remaining { get; init; }

        /// <summary>
        /// Gets the date and time when the limit will reset.
        /// </summary>
        public DateTimeOffset Reset { get; init; }

        /// <summary>
        /// Gets the rate limiting policy.
        /// </summary>
        public RateLimitPolicy? Policy { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"{Remaining} of {Limit}. Resets at {Reset.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";
            }
        }
    }

    /// <summary>
    /// The details of a rate limit policy.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record RateLimitPolicy
    {
        internal RateLimitPolicy(string policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            if (!policy.Contains(";w=", StringComparison.Ordinal))
            {
                throw new ArgumentException("policy is not in the correct format.", nameof(policy));
            }

            string[] parts = policy.Split(";w =");

            if (!int.TryParse(parts[0], out int readLimit))
            {
                throw new ArgumentException("policy read limit is not an integer.", nameof(policy));
            }

            if (!int.TryParse(parts[1], out int writeLimit))
            {
                throw new ArgumentException("policy write limit is not an integer.", nameof(policy));
            }

            Read = readLimit;
            Write = writeLimit;
        }

        internal RateLimitPolicy(int read, int write)
        {
            Read = read;
            Write = write;
        }

        /// <summary>
        /// The maximum number of reads in a limitation period.
        /// </summary>
        public int Read { get; init; }

        /// <summary>
        /// The maximum number of writes in a limitation period.
        /// </summary>
        public int Write { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Read: {Read} / Write: {Write}";
            }
        }
    }
}
