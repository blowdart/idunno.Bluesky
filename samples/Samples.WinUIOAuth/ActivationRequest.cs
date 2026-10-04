// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace Samples.WinUIOAuth;

internal sealed record ActivationRequest(bool IsLaunch, Uri? Callback = null, string? Error = null);
