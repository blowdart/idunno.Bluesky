// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using Xunit;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class HttpResultTests
    {
        [Fact]
        public void SucceededReturnsTrueWhenStatusCodeIsOK()
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
        public void BoolConvertReturnsTrueWhenStatusCodeIsOKAndResultIsNotNull()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.OK,
                Result = "test"
            };

            bool result = httpResult;

            Assert.True(result);
        }

        [Fact]
        public void BoolConvertReturnsFalseWhenStatusCodeIsOKAndResultIsNotNull()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.Forbidden,
                Result = "test"
            };

            bool result = httpResult;

            Assert.False(result);
        }

        [Fact]
        public void BoolConvertReturnsTrueWhenStatusCodeIsOKAndResultIsNull()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.OK,
                Result = null
            };

            bool result = httpResult;

            Assert.False(result);
        }

        [Fact]
        public void BoolConvertReturnsTrueWhenStatusCodeIsNotOkAndResultIsNull()
        {
            var httpResult = new AtProtoHttpResult<string>
            {
                StatusCode = HttpStatusCode.Forbidden,
                Result = null
            };

            bool result = httpResult;

            Assert.False(result);
        }
    }
}
