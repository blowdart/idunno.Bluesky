// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace idunno.AtProto.Authentication
{
    internal class DPoPMessageHandler : DelegatingHandler
    {
        private readonly string _key;
        private string? _nonce;

        public DPoPMessageHandler(string key, HttpMessageHandler innerHandler)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            ArgumentNullException.ThrowIfNull(innerHandler);

            _key = key;

            InnerHandler = innerHandler;
        }

        private void AttachDPoPToken

    }
}
