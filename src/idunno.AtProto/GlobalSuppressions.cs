// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Handle specification requires lower case normalization.", Scope = "member", Target = "~M:idunno.AtProto.Handle.#ctor(System.String,System.Boolean)")]
[assembly: SuppressMessage("Security", "CA5404:Do not disable token validation checks", Justification = "PDSs do not issue JWTs with issuers.", Scope = "member", Target = "~M:idunno.AtProto.AtProtoAgent.ValidateJwtToken(System.String,idunno.AtProto.Did,System.Uri)~System.Threading.Tasks.Task{System.Boolean}")]
[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", Justification = "Directory lookups are not an AT Protocol function and should be separated into their own namespace.", Scope = "namespace", Target = "~N:idunno.DidPlcDirectory")]
[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Handle specification requires lower case normalization.", Scope = "member", Target = "~M:idunno.AtProto.Handle.GetHashCode~System.Int32")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable to allow deserialization to create any overflow data.", Scope = "member", Target = "~P:idunno.AtProto.Repo.AtProtoRecord.ExtensionData")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable to allow deserialization to create any overflow data.", Scope = "member", Target = "~P:idunno.AtProto.Repo.AtProtoRecordValue.ExtensionData")]
[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", Justification = "Keeping the label classes matching their lexicon position.", Scope = "namespace", Target = "~N:idunno.AtProto")]
[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", Justification = "Forked due to https://github.com/IdentityModel/IdentityModel.OidcClient/issues/446", Scope = "namespace", Target = "~N:IdentityModel.OidcClient.DPoP")]
