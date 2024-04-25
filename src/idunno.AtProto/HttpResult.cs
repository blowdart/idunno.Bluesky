// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto
{
    public record HttpResult<T>
    {
        /// <summary>
        /// Gets the <see cref="HttpStatusCode" associated with the response to an HTTP request./>
        /// </summary>
        /// <value>
        /// The <see cref="HttpStatusCode" associated with the response to an HTTP request./>
        /// </value>
        public HttpStatusCode StatusCode { get; internal set; }

        public bool Succeeded {
            get
            {
                return StatusCode == HttpStatusCode.OK;
            }
        }

        /// <summary>
        /// The extended error information, if any, if the request was unsuccessful.
        /// </summary>
        public AtErrorDetail? Error {get; internal set;}

        /// <summary>
        ///The result of an HttpRequest, if the request has was successful.
        /// </summary>
        public T? Result { get; internal set; }
    }
}
