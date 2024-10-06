// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Xunit;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class AtCidTests
    {
        const string ValidCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

        [Fact]
        public void ValidAtCidValuesConstructCorrectly()
        {
            AtCid atCid = new(ValidCid);

            Assert.Equal(ValidCid, atCid.Value);
        }

        [Fact]
        public void EmptyValueConstructionShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new AtCid(string.Empty));
        }

        [Fact]
        public void ImplicitConversionFromValidStringWorks()
        {
            AtCid atCid = ValidCid;

            Assert.NotNull(atCid);
            Assert.Equal(ValidCid, atCid.Value);
        }

        [Fact]
        public void ExplicitConversionFromValidStringWorks()
        {
            AtCid atCid = (AtCid)ValidCid;

            Assert.NotNull(atCid);
            Assert.Equal(ValidCid, atCid.Value);
        }

        [Fact]
        public void EqualityWorks()
        {
            AtCid lhs = (AtCid)ValidCid;
            AtCid rhs = new(ValidCid);

            Assert.NotNull(lhs);
            Assert.NotNull(rhs);
            Assert.Equal(lhs, rhs);
            Assert.True(lhs.Equals(rhs));
        }
    }
}
