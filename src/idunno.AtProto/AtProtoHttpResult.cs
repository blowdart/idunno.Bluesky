// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

namespace idunno.AtProto
{
    /// <summary>
    /// Wraps the result from an AT Proto API or Bluesky API call into a single object that holds both the result, if any,
    /// and any error details returned by the API.
    /// </summary>
    /// <typeparam name="TResult">The type the results should be deserialized into.</typeparam>
    public class AtProtoHttpResult<TResult>
    {
        internal AtProtoHttpResult()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpResult{TResult}"/>.
        /// </summary>
        /// <param name="result">The resulting object of type <typeparamref name="TResult"/> returned by the API call, if any.</param>
        /// <param name="statusCode">The underlying HTTP status code returned by the API call.</param>
        /// <param name="httpResponseHeaders">The <see cref="HttpResponseHeaders"/> returned by the request</param>
        /// <param name="atErrorDetail">The <see cref="AtErrorDetail"/> returned by the API call, if any.</param>
        /// <param name="rateLimit">The API rate limit for the current user, if the response included one.</param>
        public AtProtoHttpResult(TResult? result, HttpStatusCode statusCode, HttpResponseHeaders? httpResponseHeaders, AtErrorDetail ? atErrorDetail = null, RateLimit? rateLimit = null)
        {
            Result = result;
            StatusCode = statusCode;
            HttpResponseHeaders = httpResponseHeaders;
            AtErrorDetail = atErrorDetail;
            RateLimit = rateLimit;
        }

        /// <summary>
        ///The result of an HttpRequest, if the request has was successful.
        /// </summary>
        public TResult? Result { get; internal set; }

        /// <summary>
        /// Gets the <see cref="HttpStatusCode" /> associated with the response to an AT Proto or Bluesky API request.
        /// </summary>
        /// <value>
        /// The <see cref="HttpStatusCode" /> associated with the response to an AT Proto or Bluesky API request.
        /// </value>
        public HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// The extended error information, if any, if the request was unsuccessful.
        /// </summary>
        public AtErrorDetail? AtErrorDetail {get; internal set; }

        /// <summary>
        /// A flag indicating if the https request returned a status code of OK or No Content and a result is present.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Result))]
        public bool Succeeded
        {
            get
            {
                return (StatusCode == HttpStatusCode.OK ||
                        StatusCode == HttpStatusCode.NoContent) && Result is not null;
            }
        }

        /// <summary>
        /// The api rate limit, if the api returned one, otherwise <see langword="null"/>.
        /// </summary>
        public RateLimit? RateLimit { get; init; }

        /// <summary>
        /// The <see cref="HttpResponseHeaders"/> returned by the request, if any.
        /// </summary>
        public HttpResponseHeaders? HttpResponseHeaders { get; internal set; }

        /// <summary>
        /// Throw an <see cref="AtProtoHttpRequestException"/> if <see cref="Succeeded"/> is <see langword="false"/>.
        /// </summary>
        /// <returns>The AtProtoHttpResult if the call succeeded.</returns>
        /// <exception cref="AtProtoHttpRequestException">The AtProtoHttpResult did not succeed.</exception>
        [MemberNotNull(nameof(Result))]
        public AtProtoHttpResult<TResult> EnsureSucceeded()
        {
            if (StatusCode != HttpStatusCode.OK && StatusCode != HttpStatusCode.NoContent)
            {
                throw new AtProtoHttpRequestException(
                    message: "Status code != OK || NoContent",
                    innerException: null,
                    statusCode: StatusCode);
            }
            else if (Result is null)
            {
                throw new AtProtoHttpRequestException(
                    message: "Result is null",
                    innerException: null);
            }

            return this;
        }
    }
}
