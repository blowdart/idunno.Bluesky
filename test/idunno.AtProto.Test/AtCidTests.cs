// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Xunit;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class AtCidTests
    {
        [Fact]
        public void ValidAtCidValuesConstructCorrectly()
        {
            const string cidAsString = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AtCid atCid = new(cidAsString);

            Assert.Equal(cidAsString, atCid.Value);
        }

        [Fact]
        public void EmptyValueConstructionShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new AtCid(string.Empty));
        }
    }
}
