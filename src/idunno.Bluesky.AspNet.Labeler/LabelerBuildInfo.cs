// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Reads the build identifier shared by the host and Xenon footer.</summary>
internal static class LabelerBuildInfo
{
    /// <summary>Returns the stamped commit identifier or the assembly's informational version.</summary>
    internal static string? GetBuildStamp(Assembly assembly) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "BuildCommitId")?.Value
            ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
