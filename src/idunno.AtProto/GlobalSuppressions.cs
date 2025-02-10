// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", Justification = "Forked due to https://github.com/IdentityModel/IdentityModel.OidcClient/issues/446", Scope = "namespace", Target = "~N:IdentityModel.OidcClient.DPoP")]
[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", Justification = "The plc functions are not part of AT Proto, but it is not worth pulling this out into its own package", Scope = "namespace", Target = "~N:idunno.DidPlcDirectory")]
