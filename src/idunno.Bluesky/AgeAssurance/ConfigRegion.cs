// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// The age-assurance configuration for a specific region.
/// </summary>
/// <param name="CountryCode">The ISO 3166-1 alpha-2 country code.</param>
/// <param name="MinAccessAge">The minimum age required to use Bluesky in this region.</param>
/// <param name="Rules">The ordered age-assurance rules for this region.</param>
/// <param name="RegionCode">The ISO 3166-2 region code, if the configuration is not country-wide.</param>
/// <param name="Platforms">The platforms to which this configuration applies, or <see langword="null"/> if it applies to all platforms.</param>
/// <param name="AdditionalVerificationMethods">Additional verification methods permitted in this region.</param>
public sealed record ConfigRegion(
    [property: JsonRequired] string CountryCode,
    [property: JsonRequired] int MinAccessAge,
    [property: JsonRequired] IReadOnlyCollection<ConfigRegionRule> Rules,
    string? RegionCode,
    IReadOnlyCollection<AgeAssurancePlatform>? Platforms,
    IReadOnlyCollection<AdditionalVerificationMethod>? AdditionalVerificationMethods);
