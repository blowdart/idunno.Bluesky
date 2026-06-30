// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto;

/// <summary>
/// Encapsulates a timestamp-based unique identifier.
/// </summary>
[JsonConverter(typeof(Json.TimestampIdentifierConverter))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed partial class TimestampIdentifier :
    IComparable<TimestampIdentifier>,
    IEquatable<TimestampIdentifier>,
    IEqualityComparer<TimestampIdentifier>,
    IParsable<TimestampIdentifier>
{
    [GeneratedRegex("^[234567abcdefghij][234567abcdefghijklmnopqrstuvwxyz]{12}$", RegexOptions.None, 100)]
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
    /// Creates a new, unique, instance of <see cref="TimestampIdentifier"/> based on the current time.
    /// </summary>
    public TimestampIdentifier()
    {
        RecordKey next = Next();
        _value = next.ToString();
    }

    /// <summary>
    /// Creates a new instance of <see cref="TimestampIdentifier"/>
    /// </summary>
    /// <param name="s">The string to create a <see cref="TimestampIdentifier"/> from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is not a valid <see cref="TimestampIdentifier"/>.</exception>
    public TimestampIdentifier(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        s = s.Trim('-');

        if (s.Length != TidLength ||
            !s_Validator().IsMatch(s))
        {
            throw new ArgumentException("not a valid TimeStampIdentifier", nameof(s));
        }

        _value = s;
    }

    /// <summary>
    /// Creates a new instance of <see cref="TimestampIdentifier"/>
    /// </summary>
    /// <param name="recordKey">The <see cref="RecordKey"/> to create a timestamp from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="recordKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="recordKey"/> is not a valid <see cref="TimestampIdentifier"/></exception>
    public TimestampIdentifier(RecordKey recordKey)
    {
        ArgumentNullException.ThrowIfNull(recordKey);

        if (recordKey.ToString().Length != TidLength || !s_Validator().IsMatch(recordKey.ToString()))
        {
            throw new ArgumentException("not a valid TimeStampIdentifier", nameof(recordKey));
        }

        _value = recordKey.ToString();
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
    /// Creates a unique <see cref="TimestampIdentifier"/> from the current time.
    /// </summary>
    /// <returns>A unique <see cref="TimestampIdentifier"/> from the current time.</returns>
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Not a cryptographic function.")]
    public static TimestampIdentifier Next()
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

        return FromTime(timeStamp, (long)s_clockId.Value);
    }

    /// <summary>
    /// Returns a string that represents the current <see cref="TimestampIdentifier"/> object.
    /// </summary>
    /// <returns>A string representation of the current <see cref="TimestampIdentifier"/>.</returns>
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
    public static implicit operator TimestampIdentifier(string s) => FromString(s);

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
    /// Converts the specified <see cref="TimestampIdentifier"/> to a <see cref="RecordKey"/>.
    /// </summary>
    /// <param name="tid">The <see cref="TimestampIdentifier"/> to convert.</param>
    /// <returns>A <see cref="RecordKey"/> representing the specified <see cref="TimestampIdentifier"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tid"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator RecordKey([DisallowNull] TimestampIdentifier tid)
    {
        ArgumentNullException.ThrowIfNull(tid);

        return new(tid.ToString());
    }

    /// <summary>
    /// Creates a <see cref="TimestampIdentifier"/> from the specified record key.
    /// </summary>
    /// <param name="rKey">The record key to convert.</param>
    /// <returns>A <see cref="TimestampIdentifier"/> from the specified record key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rKey"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimestampIdentifier FromRecordKey([DisallowNull] RecordKey rKey) => new(rKey);

    /// <summary>
    /// Creates a <see cref="TimestampIdentifier"/> from the specified record key.
    /// </summary>
    /// <param name="rKey">The record key to convert.</param>
    /// <returns>A <see cref="TimestampIdentifier"/> from the specified record key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TimestampIdentifier([DisallowNull] RecordKey rKey) => FromRecordKey(rKey);

    /// <summary>
    /// Returns a string that represents the current <see cref="TimestampIdentifier"/>.
    /// </summary>
    /// <returns>A string that represents the current <see cref="TimestampIdentifier"/>.</returns>
    public RecordKey ToRecordKey() => new(_value);

    /// <summary>
    /// Parses a string into a <see cref="TimestampIdentifier"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A <see cref="TimestampIdentifier"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the string is not a valid <see cref="TimestampIdentifier"/>.</exception>
    public static TimestampIdentifier Parse(string s)
    {
        return Parse(s, provider: null);
    }

    /// <summary>
    /// Parses a string into a <see cref="TimestampIdentifier"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>A <see cref="TimestampIdentifier"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the string is not a valid <see cref="TimestampIdentifier"/>.</exception>
    public static TimestampIdentifier Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);

        s = s.Trim('-');
        if (s.Length != TidLength ||
            !s_Validator().IsMatch(s))
        {
            throw new FormatException("Invalid format");
        }

        return new TimestampIdentifier(s);
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="TimestampIdentifier"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">The parsed <see cref="TimestampIdentifier"/> if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, [MaybeNullWhen(false)] out TimestampIdentifier result)
    {
        return TryParse(s, provider: null, out result);
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="TimestampIdentifier"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="result">The parsed <see cref="TimestampIdentifier"/> if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error catchall to enable TryParse pattern")]
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TimestampIdentifier result)
    {
        result = null;

        if (s is null)
        {
            return false;
        }

        try
        {
            result = Parse(s, provider);
            return true;

        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns a hash code for the specified <see cref="TimestampIdentifier"/> object.
    /// </summary>
    /// <param name="obj">The <see cref="TimestampIdentifier"/> object.</param>
    /// <returns>A hash code for the specified <see cref="TimestampIdentifier"/> object.</returns>
    public int GetHashCode([DisallowNull] TimestampIdentifier obj) => Comparer.GetHashCode(obj);

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code for this instance.</returns>
    public override int GetHashCode() => Comparer.GetHashCode(this);

    /// <summary>
    /// Determines whether two <see cref="TimestampIdentifier"/> objects are equal, or a <see cref="TimestampIdentifier"/> object is equal to a specified object.
    /// </summary>
    /// <param name="x">The first <see cref="TimestampIdentifier"/> to compare.</param>
    /// <param name="y">The second <see cref="TimestampIdentifier"/> to compare.</param>
    /// <returns><see langword="true"/> if the objects are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(TimestampIdentifier? x, TimestampIdentifier? y) => Comparer.Equals(x, y);

    /// <summary>
    /// Determines whether the current <see cref="TimestampIdentifier"/> object is equal to another object.
    /// </summary>
    /// <param name="other">The <see cref="TimestampIdentifier"/> to compare with the current object.</param>
    /// <returns><see langword="true"/> if the current <see cref="TimestampIdentifier" /> is equal to the <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(TimestampIdentifier? other) => Equals(this, other);

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns><see langword="true"/> if the specified object is equal to the current <see cref="TimestampIdentifier"/>; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is null)
        {
            return false;
        }

        if (obj is not TimestampIdentifier other)
        {
            return false;
        }

        return Equals(this, other);
    }

    /// <summary>
    /// Compares this instance to a specified <see cref="TimestampIdentifier"/> object and returns an indication of their relative values.
    /// </summary>
    /// <param name="other">The <see cref="TimestampIdentifier"/> to compare with the current object.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(TimestampIdentifier? other)
    {
        // TID string values are meant for comparison, so just use the string comparison of the underlying values.
        return _value.CompareTo(other?._value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Determines whether two <see cref="TimestampIdentifier"/> objects are equal.</summary>
    /// <param name="left">The first <see cref="TimestampIdentifier"/> to compare.</param>
    /// <param name="right">The second <see cref="TimestampIdentifier"/> to compare.</param>
    /// <returns><see langword="true"/> if the objects are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(TimestampIdentifier left, TimestampIdentifier right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="TimestampIdentifier"/> objects are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="TimestampIdentifier"/> to compare.</param>
    /// <param name="right">The second <see cref="TimestampIdentifier"/> to compare.</param>
    /// <returns><see langword="true"/> if the objects are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(TimestampIdentifier left, TimestampIdentifier right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Compares two <see cref="TimestampIdentifier"/> objects and returns a value indicating whether one is less than, equal to, or greater than the other.
    /// </summary>
    /// <param name="left">The first <see cref="TimestampIdentifier"/> to compare.</param>
    /// <param name="right">The second <see cref="TimestampIdentifier"/> to compare.</param>
    /// <returns><see langword="true"/> if the first object is less than the second; otherwise, <see langword="false"/>.</returns>
    public static bool operator <(TimestampIdentifier left, TimestampIdentifier right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Compares two <see cref="TimestampIdentifier"/> objects and returns a value indicating whether one is greater than, equal to, or less than the other.
    /// </summary>
    /// <param name="left">The first <see cref="TimestampIdentifier"/> to compare.</param>
    /// <param name="right">The second <see cref="TimestampIdentifier"/> to compare.</param>
    /// <returns><see langword="true"/> if the first object is less than or equal to the second; otherwise, <see langword="false"/>.</returns>
    public static bool operator <=(TimestampIdentifier left, TimestampIdentifier right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Compares two <see cref="TimestampIdentifier"/> objects and returns a value indicating whether one is greater than the other.
    /// </summary>
    /// <param name="left">The first <see cref="TimestampIdentifier"/> to compare.</param>
    /// <param name="right">The second <see cref="TimestampIdentifier"/> to compare.</param>
    /// <returns><see langword="true"/> if the first object is greater than the second; otherwise, <see langword="false"/>.</returns>
    public static bool operator >(TimestampIdentifier left, TimestampIdentifier right)
    {
        return left is not null && left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Compares two <see cref="TimestampIdentifier"/> objects and returns a value indicating whether one is greater than or equal to the other.
    /// </summary>
    /// <param name="left">The first <see cref="TimestampIdentifier"/> to compare.</param>
    /// <param name="right">The second <see cref="TimestampIdentifier"/> to compare.</param>
    /// <returns><see langword="true"/> if the first object is greater than or equal to the second; otherwise, <see langword="false"/>.</returns>
    public static bool operator >=(TimestampIdentifier left, TimestampIdentifier right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Gets a <see cref="IEqualityComparer{T}"/> that compares two <see cref="TimestampIdentifier"/> objects for equivalence.
    /// </summary>
    public static IEqualityComparer<TimestampIdentifier> Comparer { get; } = new TimestampIdentifierEqualityComparer();

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

    private sealed class TimestampIdentifierEqualityComparer : IEqualityComparer<TimestampIdentifier>
    {
        public bool Equals(TimestampIdentifier? x, TimestampIdentifier? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x._value == y._value;
        }

        public int GetHashCode(TimestampIdentifier obj) => obj._value.GetHashCode(StringComparison.InvariantCulture);
    }
}