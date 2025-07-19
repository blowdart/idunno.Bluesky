// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto.Test
{
    public class HttpResultTests
    {
        [Fact]
        public void SucceededReturnsTrueWhenStatusCodeIsOKAndResultIsPresent()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.OK,
                Result = "test"
            };

            bool result = httpResult.Succeeded;

            Assert.True(result);
        }

        [Fact]
        public void SucceededReturnsFalseWhenStatusCodeIsOKAndResultIsNull()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.OK,
                Result = null
            };

            bool result = httpResult.Succeeded;

            Assert.False(result);
        }


        [Fact]
        public void SucceededReturnsFalseWhenStatusCodeIsNotOK()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.Forbidden,
                Result = "test"
            };

            bool result = httpResult.Succeeded;

            Assert.False(result);
        }

        [Fact]
        public void SucceededWithResultReturnsTrueWhenStatusCodeIsOKAndAResultIsPresent()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.OK,
                Result = "test"
            };

            bool result = httpResult.Succeeded;

            Assert.True(result);
        }

        [Fact]
        public void SucceededWithResultReturnsFalseWhenStatusCodeIsOKAndButResultIsNull()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.OK,
                Result = null
            };

            bool result = httpResult.Succeeded;

            Assert.False(result);
        }
    }
}
