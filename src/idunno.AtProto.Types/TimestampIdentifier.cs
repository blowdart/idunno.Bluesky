// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto
{
    /// <summary>
    /// Utility class for Timestamp Identifiers (TID)
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed partial class TimestampIdentifier
    {
        [GeneratedRegex("^[234567abcdefghij][234567abcdefghijklmnopqrstuvwxyz]{12}$", RegexOptions.IgnoreCase, 100)]
        private static partial Regex s_Validator();

        private const int TidLength = 13;

        private static double? s_clockId;

        private static readonly Random s_random = new();

        private static double s_lastTimeStamp;

#if NET9_0_OR_GREATER
        private static readonly Lock s_syncLock = new ();
#else
        private static readonly object s_syncLock = new();
#endif

        private readonly string _value;

        /// <summary>
        /// Creates a new instance of <see cref="TimestampIdentifier"/>
        /// </summary>
        /// <param name="s">The string to create a <see cref="TimestampIdentifier"/> from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="s"/> is not the correct length for a <see cref="TimestampIdentifier"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is not a valid <see cref="TimestampIdentifier"/>.</exception>
        public TimestampIdentifier(string s)
        {
            ArgumentNullException.ThrowIfNull(s);

            s = s.Trim('-');

            ArgumentOutOfRangeException.ThrowIfNotEqual(s.Length, TidLength);

            if (!s_Validator().IsMatch(s))
            {
                throw new ArgumentException("not a valid TimeStampIdentifier", nameof(s));
            }

            _value = s;
        }

        /// <summary>
        /// Creates a <see cref="RecordKey"/> from the specified <paramref name="timeStamp"/> and <paramref name="clockId"/>.
        /// </summary>
        /// <param name="timeStamp">The timestamp to generate the <see cref="RecordKey"/> from.</param>
        /// <param name="clockId">The clock identifier to generate the <see cref="RecordKey"/> from.</param>
        /// <returns></returns>
        internal static string FromTime(double timeStamp, long clockId)
        {
            string encodedTimeStamp = SortableBase32Encoding.ToString(timeStamp).PadLeft(11, '2');
            string encodedClockId = SortableBase32Encoding.ToString(clockId).PadLeft(2, '2');

            return $"{encodedTimeStamp}{encodedClockId}";
        }

        /// <summary>
        /// Creates a unique <see cref="RecordKey"/> from the current time.
        /// </summary>
        /// <returns>A unique <see cref="RecordKey"/> from the current time.</returns>
        [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Not a cryptographic function.")]
        public static RecordKey Next()
        {
            lock (s_syncLock)
            {
                s_clockId ??= Math.Floor(s_random.NextSingle() * 32);
            }

            TimeSpan duration = DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch;
            double microsecondsSinceEpoch = Math.Round(duration.TotalMicroseconds);

            // - monotonically increasing time
            double timeStamp = Math.Max(microsecondsSinceEpoch, s_lastTimeStamp);
            lock (s_syncLock)
            {
                s_lastTimeStamp = timeStamp + 1;
            }

            return new RecordKey(FromTime(timeStamp, (long)s_clockId.Value));
        }

        /// <inheritdoc />
        public override string ToString() => $"{_value}";

        /// <summary>
        /// Creates a <see cref="TimestampIdentifier"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A <see cref="TimestampIdentifier"/> from the specified string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimestampIdentifier FromString(string s) => new(s);

        /// <summary>
        /// Creates a <see cref="TimestampIdentifier"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A <see cref="TimestampIdentifier"/> from the specified string.</returns>
        public static implicit operator TimestampIdentifier(string s) => TimestampIdentifier.FromString(s);

        /// <summary>
        /// Converts the specified <see cref="TimestampIdentifier"/> to a string.
        /// </summary>
        /// <param name="tid">The <see cref="TimestampIdentifier"/> to convert.</param>
        /// <returns>A <see cref="TimestampIdentifier"/> from the specified string.</returns>
        public static implicit operator string(TimestampIdentifier tid)
        {
            if (tid is null)
            {
                return string.Empty;
            }
            else
            {
                return tid.ToString();
            }
        }

        /// <summary>
        /// Gets the value of this instance of <see cref="TimestampIdentifier"/>.
        /// </summary>
        [JsonPropertyName("tid")]
        public string Value => ToString();

        /// <summary>
        /// Gets the timestamp of the current instance of <see cref="TimestampIdentifier"/>.
        /// </summary>
        [JsonIgnore]
        public double TimeStamp => SortableBase32Encoding.FromString(_value.Substring(0, 11));

        /// <summary>
        /// Gets the clock id of the current instance of <see cref="TimestampIdentifier"/>.
        /// </summary>
        [JsonIgnore]
        public double ClockId => SortableBase32Encoding.FromString(_value.Substring(11, 2));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => ToString();
    }
}
