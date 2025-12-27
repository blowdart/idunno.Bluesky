// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Types.Test
{
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
            Assert.Throws<ArgumentException>(() => new Cid(string.Empty));
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

        [Fact]
        public void ConstructingFromPropertiesProducesSameStringRepresentation()
        {
            Cid atCidFromString = ValidCid;

            Cid atCidFromProperties = new (atCidFromString.Version, atCidFromString.Codec, [.. atCidFromString.Hash]);

            Assert.NotNull(atCidFromString);
            Assert.NotNull(atCidFromProperties);

            Assert.Equal(atCidFromString.Version, atCidFromProperties.Version);
            Assert.Equal(atCidFromString.Codec, atCidFromProperties.Codec);
            Assert.Equal(atCidFromString.Hash, atCidFromProperties.Hash);
            Assert.Equal(atCidFromString.ToString(), atCidFromProperties.ToString());
        }
    }
}
