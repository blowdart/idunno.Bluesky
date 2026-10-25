// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Issuance;

namespace idunno.Bluesky.Labeler.Host;

/// <summary>Provides the host's configured declaration and issuance permissions as one immutable policy.</summary>
internal sealed class ConfigurationLabelIssuancePolicyProvider : ILabelIssuancePolicyProvider
{
    private readonly LabelIssuancePolicy _policy;

    /// <summary>Initializes a new instance of the <see cref="ConfigurationLabelIssuancePolicyProvider"/> class.</summary>
    /// <param name="configuration">The host's declared labels and target permissions.</param>
    /// <exception cref="InvalidOperationException">No label values are configured.</exception>
    internal ConfigurationLabelIssuancePolicyProvider(IConfiguration configuration)
    {
        string[] values = configuration["Labeler:DeclaredLabels"]?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (values.Length == 0)
        {
            throw new InvalidOperationException("Configure at least one value in Labeler:DeclaredLabels.");
        }

        _policy = new(
            version: "configuration-v1",
            declaredValues: values,
            allowAccounts: configuration.GetValue<bool>("Labeler:AllowAccountLabels"),
            allowRecords: configuration.GetValue<bool>("Labeler:AllowRecordLabels"));
    }

    /// <inheritdoc/>
    public Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LabelServiceResult<LabelIssuancePolicy>.Success(_policy));
    }
}
