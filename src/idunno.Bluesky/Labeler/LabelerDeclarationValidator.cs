// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler;

/// <summary>
/// Validates declarations at the publishing boundary without restricting deserialization of server data.
/// </summary>
internal static class LabelerDeclarationValidator
{
    internal static void Validate(Service declaration)
    {
        LabelerPolicies policies = declaration.Policies;
        ArgumentNullException.ThrowIfNull(policies);

        LabelPolicyValidator.Validate(policies.LabelValues, policies.LabelValueDefinitions);

        if (declaration.ReasonTypes is not null)
        {
            foreach (string reason in declaration.ReasonTypes)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            }
        }

        if (declaration.SubjectTypes is not null)
        {
            foreach (string subject in declaration.SubjectTypes)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(subject);
            }
        }

        if (declaration.SubjectCollections is not null)
        {
            foreach (Nsid collection in declaration.SubjectCollections)
            {
                ArgumentNullException.ThrowIfNull(collection);
            }
        }
    }
}
