// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>
/// Runs the seeded Xenon host scenario for each combination of trusted proxy and legacy policy
/// in its own class so xUnit executes the combinations in parallel.
/// </summary>
public class XenonSeededHostDirectCurrentPolicyTests
{
    [Fact]
    public Task SeededHostPreservesStateAndFiltersHostsBeforeMetadata() =>
        XenonTestHostTests.RunSeededHostScenarioAsync(trusted: false, legacyPolicy: false);
}

/// <summary>Runs the seeded Xenon host scenario behind a trusted proxy with the current policy.</summary>
public class XenonSeededHostTrustedCurrentPolicyTests
{
    [Fact]
    public Task SeededHostPreservesStateAndFiltersHostsBeforeMetadata() =>
        XenonTestHostTests.RunSeededHostScenarioAsync(trusted: true, legacyPolicy: false);
}

/// <summary>Runs the seeded Xenon host scenario without a trusted proxy from the legacy policy.</summary>
public class XenonSeededHostDirectLegacyPolicyTests
{
    [Fact]
    public Task SeededHostPreservesStateAndFiltersHostsBeforeMetadata() =>
        XenonTestHostTests.RunSeededHostScenarioAsync(trusted: false, legacyPolicy: true);
}

/// <summary>Runs the seeded Xenon host scenario behind a trusted proxy from the legacy policy.</summary>
public class XenonSeededHostTrustedLegacyPolicyTests
{
    [Fact]
    public Task SeededHostPreservesStateAndFiltersHostsBeforeMetadata() =>
        XenonTestHostTests.RunSeededHostScenarioAsync(trusted: true, legacyPolicy: true);
}
