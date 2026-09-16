// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace idunno.AtProto.Types.Test;

// The clock identifier test manipulates static generator state, so these tests must not
// run in parallel with the other TimestampIdentifier tests.
[Collection("TimestampIdentifier")]
public class TimestampIdentifierSpecTests
{
    [Theory]
    [InlineData("-3lwjaurx2d22c")]
    [InlineData("3lwjaurx2d22c-")]
    [InlineData("-3lwjaurx2d22c-")]
    [InlineData("--3lwjaurx2d22c--")]
    public void HyphenatedTimestampIdentifiersAreRejected(string value)
    {
        // https://atproto.com/specs/tid#tid-syntax
        // "Early versions of the TID syntax allowed hyphens, but they are no longer allowed and should be rejected when parsing."

        Assert.Throws<ArgumentException>(() => new TimestampIdentifier(value));
        Assert.False(TimestampIdentifier.TryParse(value, out TimestampIdentifier? _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(512)]
    [InlineData(1023)]
    public void ClockIdentifierRoundTripsAcrossTheFullTenBitRange(long clockId)
    {
        TimestampIdentifier tid = TimestampIdentifier.FromString(TimestampIdentifier.FromTime(0, clockId));

        Assert.Equal(clockId, tid.ClockId);
    }

    [Fact]
    public void NextGeneratesClockIdentifiersAcrossTheFullTenBitRange()
    {
        // https://atproto.com/specs/tid#tid-structure - "The final 10 bits are a random clock identifier."
        // A five bit clock identifier can never exceed 31, so observing a larger value proves the full range is in use.

        FieldInfo clockIdField =
            typeof(TimestampIdentifier).GetField("s_clockId", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.NotNull(clockIdField);

        object? original = clockIdField.GetValue(null);
        double highest = 0;

        try
        {
            for (int i = 0; i < 500; i++)
            {
                clockIdField.SetValue(null, null);
                highest = Math.Max(highest, TimestampIdentifier.Next().ClockId);
            }
        }
        finally
        {
            clockIdField.SetValue(null, original);
        }

        Assert.True(highest > 31, $"The highest clock identifier seen over 500 samples was {highest}.");
        Assert.True(highest <= 1023, $"The highest clock identifier seen over 500 samples was {highest}.");
    }
}
