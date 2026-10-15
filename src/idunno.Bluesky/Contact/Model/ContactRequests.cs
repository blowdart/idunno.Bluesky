// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Contact.Model;

internal sealed record StartPhoneVerificationRequest(string Phone);

internal sealed record VerifyPhoneRequest(string Phone, string Code);

internal sealed record ImportContactsRequest(string Token, IReadOnlyList<string> Contacts);

internal sealed record DismissContactMatchRequest(Did Subject);
