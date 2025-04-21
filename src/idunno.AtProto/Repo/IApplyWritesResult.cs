// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Marker interface for the results of an applyWrites operation.
    /// </summary>
    [SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "There is no commonality between the classes, except for the API that produces them.")]
    public interface IApplyWritesResult
    {
    }
}
