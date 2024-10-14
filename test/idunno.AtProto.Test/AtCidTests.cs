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
            Cid atCid = new(ValidCid);

            Assert.Equal(ValidCid, atCid.Value);
        }

        [Fact]
        public void EmptyValueConstructionShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new Cid(string.Empty));
        }

        [Fact]
        public void ImplicitConversionFromValidStringWorks()
        {
            Cid atCid = ValidCid;

            Assert.NotNull(atCid);
            Assert.Equal(ValidCid, atCid.Value);
        }

        [Fact]
        public void ExplicitConversionFromValidStringWorks()
        {
            Cid atCid = (Cid)ValidCid;

            Assert.NotNull(atCid);
            Assert.Equal(ValidCid, atCid.Value);
        }

        [Fact]
        public void EqualityWorks()
        {
            Cid lhs = (Cid)ValidCid;
            Cid rhs = new(ValidCid);

            Assert.NotNull(lhs);
            Assert.NotNull(rhs);
            Assert.Equal(lhs, rhs);
            Assert.True(lhs.Equals(rhs));
        }
    }
}
