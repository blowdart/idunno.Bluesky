// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Enum indicating the validation status of record creation.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ValidationStatus>))]
    public enum ValidationStatus
    {
        /// <summary>
        /// Validation status is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Validation status is valid.
        /// </summary>
        Valid
    }
}
