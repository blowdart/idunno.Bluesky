// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

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
        public void TimestampIdentifierNextMeetsSpec()
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

        [Fact]
        public void TimestampIdentifierMeetsSpec()
        {
            var actual = new TimestampIdentifier();

            Assert.Matches("""^[234567abcdefghij][234567abcdefghijklmnopqrstuvwxyz]{12}$""", actual.ToString());
        }

        [Fact]
        public void ImplicitConversionToRecordKeyWorks()
        {
            var tid = new TimestampIdentifier();
            RecordKey rk = (RecordKey)tid;
            Assert.Equal(tid.ToString(), rk.ToString());
        }

        [Fact]
        public void ExplicitConversionToRecordKeyWorks()
        {
            var tid = new TimestampIdentifier();
            RecordKey rk = tid.ToRecordKey();
            Assert.Equal(tid.ToString(), rk.ToString());
        }

        [Fact]
        public void TimestampIdentifierConstructorThrowsOnNullString()
        {
            Assert.Throws<ArgumentNullException>(() => new TimestampIdentifier(null!));
        }

        [Fact]
        public void TimestampIdentifierConstructorThrowsOnInvalidLengthString()
        {
            Assert.Throws<ArgumentException>(() => new TimestampIdentifier("short"));
        }

        [Fact]
        public void TimestampIdentifierConstructorThrowsOnInvalidFormatString()
        {
            Assert.Throws<ArgumentException>(() => new TimestampIdentifier("invalidFormat"));
        }

        [Fact]
        public void TimestampIdentifierConstructorFromRecordKeyWorks()
        {
            var expected = new RecordKey("3lwjaurx2d22c");

            var actual = new TimestampIdentifier(expected);
            Assert.Equal(expected.ToString(), actual.ToString());
        }

        // Values taken from https://atproto.com/specs/tid#examples
        [Theory]
        [InlineData("3jzfcijpj2z2a")]
        [InlineData("7777777777777")]
        [InlineData("3zzzzzzzzzzzz")]
        [InlineData("2222222222222")]
        public void SyntacticallyValidTimestampIdentifiersAreAccepted(string tidString)
        {
            var tid = new TimestampIdentifier(tidString);
            Assert.Equal(tidString, tid.ToString());
        }

        [Theory]
        [InlineData("3jzfcijpj2z21")]
        [InlineData("0000000000000")]
        [InlineData("3JZFCIJPJ2Z2A")]
        [InlineData("3jzfcijpj2z2aa")]
        [InlineData("3jzfcijpj2z2")]
        [InlineData("222")]
        [InlineData("zzzzzzzzzzzzz")]
        [InlineData("kjzfcijpj2z2a")]
        public void InvalidTimestampIdentifiersCannotBeConstructed(string tidAsString)
        {
            Assert.Throws<ArgumentException>(() => _ = new TimestampIdentifier(tidAsString));
        }

        [Fact]
        public void TimestampIdentifierDeserializationWorks()
        {
            string json = """{"tid":"3lwjaurx2d22c"}""";

            var expected = new TimestampIdentifier("3lwjaurx2d22c");

            TimestampIdentifierRecord? actual = System.Text.Json.JsonSerializer.Deserialize<TimestampIdentifierRecord>(json, System.Text.Json.JsonSerializerOptions.Web);

            Assert.Equal(actual!.Tid.Value, expected.Value);
        }

    }

    internal record TimestampIdentifierRecord
    {
        [JsonRequired]
        public required TimestampIdentifier Tid { get; init; }
    }
}
