// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Normalization for handles is to convert to lowercase.", Scope = "member", Target = "~M:idunno.AtProto.Handle.#ctor(System.String,System.Boolean)")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Property wraps an individual entry in the values collection.", Scope = "member", Target = "~P:idunno.AtProto.Bluesky.NewBlueskyPost.Facets")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Property wraps an individual entry in the values collection.", Scope = "member", Target = "~P:idunno.AtProto.Bluesky.NewBlueskyPost.Languages")]
[assembly: SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Not dead code, thread guarding.", Scope = "member", Target = "~M:idunno.AtProto.AtProtoHttpClient`1.GetHttpClient(System.Net.Http.HttpClientHandler)~System.Net.Http.HttpClient")]
[assembly: SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Signatures are byte arrays and will be manipulated as a byte array.", Scope = "type", Target = "~T:idunno.AtProto.Bluesky.Label")]
[assembly: SuppressMessage("Security", "CA5404:Do not disable token validation checks", Justification = "Only parsing the JWT, validation is a server function.", Scope = "member", Target = "~F:idunno.AtProto.AtProtoAgent._defaultTokenValidationParameters")]
