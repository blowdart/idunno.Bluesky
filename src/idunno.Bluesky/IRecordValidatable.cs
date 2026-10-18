// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

namespace idunno.Bluesky;

internal interface IRecordValidatable
{
    IEnumerable<ValidationResult> ValidateRecord();
}
