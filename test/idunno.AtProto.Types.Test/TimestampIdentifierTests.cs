// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Types.Test
{
    public class TimestampIdentifierTests
    {
        [Fact]
        public void TimestampIdentifierReturnsExpectedValueForZeroTimestampAndClockId()
        {
            // https://atproto.com/specs/tid#tid-structure

            string actual = TimestampIdentifier.FromTime(0, 0);

            Assert.Equal("2222222222222", actual);
        }

        [Fact]
        public void TimestampIdentifierMeetsSpec()
        {
            RecordKey now = TimestampIdentifier.Next();

            Assert.Matches("""^[234567abcdefghij][234567abcdefghijklmnopqrstuvwxyz]{12}$""", now.ToString());
        }

        [Fact]
        public void SubsequentTimestampIdentifiersDoNotMatch()
        {
            RecordKey then = TimestampIdentifier.Next();
            RecordKey now = TimestampIdentifier.Next();

            Assert.NotEqual(then, now);
        }

        [Theory]
        [InlineData(1755343293940000, 8, "3lwjaurx2d22c")]
        [InlineData(1755343435789000, 22, "3lwjayz7wqc2q")]
        [InlineData(1755343492580000, 19, "3lwjb2pf2p22n")]
        [InlineData(1755343971911000, 28, "3lwjbiyj3es2w")]
        public void CreateRawMatchesTypescriptImplementation(double timeStamp, long clockId, string expected)
        {
            string actual = TimestampIdentifier.FromTime(timeStamp, clockId);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1755342733117000, "3lwjae343mc")]
        [InlineData(1755343244178000, "3lwjatcigmk")]
        [InlineData(1755343293940000, "3lwjaurx2d2")]
        [InlineData(1755343435789000, "3lwjayz7wqc")]
        public void Base32EncodingTimestampMatchesTypescriptImplementation(long timestamp, string expected)
        {
            string actual = SortableBase32Encoding.ToString(timestamp);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1, "3")]
        [InlineData(6, "a")]
        [InlineData(8, "c")]
        [InlineData(12, "g")]
        [InlineData(13, "h")]
        [InlineData(18, "m")]
        [InlineData(19, "n")]
        [InlineData(22, "q")]
        [InlineData(27, "v")]
        [InlineData(28, "w")]
        public void Base32EncodingClockIdMatchesTypescriptImplementation(long clockId, string expected)
        {
            string actual = SortableBase32Encoding.ToString(clockId);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1755343293940000, 8, "3lwjaurx2d22c")]
        [InlineData(1755343435789000, 22, "3lwjayz7wqc2q")]
        [InlineData(1755343492580000, 19, "3lwjb2pf2p22n")]
        [InlineData(1755343971911000, 28, "3lwjbiyj3es2w")]
        public void ValuesRoundtripCorrectly(double timeStamp, long clockId, string expected)
        {
            string actual = TimestampIdentifier.FromTime(timeStamp, clockId);
            TimestampIdentifier tid = new(actual);

            Assert.Equal(expected, tid.ToString());
            Assert.Equal(timeStamp, tid.TimeStamp);
            Assert.Equal(clockId, tid.ClockId);
        }
    }
}
