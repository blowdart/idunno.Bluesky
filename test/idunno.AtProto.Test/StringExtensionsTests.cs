// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class StringExtensionsTests
    {
        [Theory]
        [InlineData("eee", 'e', 3)]
        [InlineData("eep", 'e', 2)]
        [InlineData("pep", 'e', 1)]
        [InlineData("eee", 'a', 0)]
        [InlineData("pep", 'p', 2)]
        [InlineData("", 'p', 0)]
        public void OccurrenceCountShouldReturnTheCorrectNumberOfCharOccurrences(string toSearch, char toSearchFor, int count)
        {
            Assert.Equal(count, InternalStringExtensions.OccurrenceCount(toSearch, toSearchFor));
        }

        [Theory]
        [InlineData("eee", "e", 3)]
        [InlineData("eep", "e", 2)]
        [InlineData("pep", "e", 1)]
        [InlineData("eee", "a", 0)]
        [InlineData("pep", "p", 2)]
        [InlineData("", "p", 0)]
        [InlineData("eee", "ee", 1)]
        [InlineData("eeee", "ee", 2)]
        [InlineData("eeeee", "ee", 2)]
        public void OccurrenceCountShouldReturnTheCorrectNumberOfStringOccurrences(string toSearch, string toSearchFor, int count)
        {
            Assert.Equal(count, InternalStringExtensions.OccurrenceCount(toSearch, toSearchFor));
        }

        [Theory]
        [InlineData("a")]
        [InlineData("A")]
        [InlineData("z")]
        [InlineData("Z")]
        [InlineData("az")]
        [InlineData("AZ")]
        [InlineData("za")]
        [InlineData("ZA")]
        [InlineData("")]
        public void IsOnlyAsciiLettersShouldReturnTrueWhenStringConsistsOfOnlyAsciiLetters(string s)
        {
            Assert.True(s.IsOnlyAsciiLetters());
        }

        [Theory]
        [InlineData("1")]
        [InlineData("11")]
        [InlineData("a1")]
        [InlineData("1a")]
        [InlineData("a1a")]
        [InlineData("Z1")]
        [InlineData("1Z")]
        [InlineData("a1Z")]
        [InlineData("a-z")]
        public void IsOnlyAsciiLettersShouldReturnFalseWhenStringDoesNotConsistOfOnlyAsciiLetters(string s)
        {
            Assert.False(s.IsOnlyAsciiLetters());
        }
    }
}
