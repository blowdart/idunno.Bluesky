// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

document.querySelectorAll(".video-preview").forEach(preview => {
    const video = preview.querySelector("video");
    const error = preview.querySelector("[data-video-error]");
    video.addEventListener("error", () => {
        error.hidden = false;
    });
});
