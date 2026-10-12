// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// Additional metadata used to compute age-assurance state on a client.
/// </summary>
/// <param name="AccountCreatedAt">The account creation timestamp, if available.</param>
public sealed record StateMetadata(DateTimeOffset? AccountCreatedAt);
