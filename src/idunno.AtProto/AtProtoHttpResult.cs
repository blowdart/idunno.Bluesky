// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

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
        /// <param name="statusCode">The underlying HTTP status code returned by the API call.</param>
        /// <param name="result">The resulting object of type <typeparamref name="TResult"/> returned by the API call, if any.</param>
        /// <param name="atErrorDetail">The <see cref="AtErrorDetail"/> returned by the API call, if any.</param>
        /// <param name="rateLimit">The API rate limit for the current user, if the response included one.</param>
        public AtProtoHttpResult(TResult? result, HttpStatusCode statusCode, AtErrorDetail? atErrorDetail = null, RateLimit? rateLimit = null)
        {
            Result = result;
            StatusCode = statusCode;
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
        /// 
        public AtErrorDetail? AtErrorDetail {get; internal set; }

        /// <summary>
        /// A flag indicating if the https request returned a status code of OK and a result is present.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Result))]
        public bool Succeeded
        {
            get
            {
                return StatusCode == HttpStatusCode.OK && Result is not null;
            }
        }

        /// <summary>
        /// The api rate limit, if the api returned one, otherwise null.
        /// </summary>
        public RateLimit? RateLimit { get; init; }
    }
}
