// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Xunit;

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
        public void OccurenceCountShouldReturnTheCorrectNumberOfCharOccurences(string toSearch, char toSearchFor, int count)
        {
            Assert.Equal(count, StringExtensions.OccurrenceCount(toSearch, toSearchFor));
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
        public void OccurenceCountShouldReturnTheCorrectNumberOfStringOccurences(string toSearch, string toSearchFor, int count)
        {
            Assert.Equal(count, StringExtensions.OccurrenceCount(toSearch, toSearchFor));
        }
    }
}
