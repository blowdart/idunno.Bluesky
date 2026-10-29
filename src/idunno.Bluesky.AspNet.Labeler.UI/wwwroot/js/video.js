// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

document.querySelectorAll(".video-preview").forEach(preview => {
    const open = preview.querySelector("[data-video-open]");
    const close = preview.querySelector("[data-video-close]");
    const dialog = preview.querySelector("dialog");
    const video = preview.querySelector("video");
    const error = preview.querySelector("[data-video-error]");
    const resize = () => {
        const width = video.videoWidth || Number(open.dataset.videoWidth);
        const height = video.videoHeight || Number(open.dataset.videoHeight);
        const ratio = Number.isFinite(width) && Number.isFinite(height) && width > 0 && height > 0
            ? width / height : 16 / 9;
        const maxWidth = Math.max(1, Math.min(window.innerWidth * .9 - 64, 960));
        const maxHeight = Math.max(1, window.innerHeight * .9 - 112);
        video.style.width = `${Math.min(maxWidth, maxHeight * ratio)}px`;
        video.style.aspectRatio = String(ratio);
    };

    open.addEventListener("click", () => {
        error.hidden = true;
        resize();
        dialog.showModal();
        video.src = open.dataset.videoUrl;
    });
    close.addEventListener("click", () => dialog.close());
    dialog.addEventListener("close", () => {
        video.pause();
        video.removeAttribute("src");
        video.load();
        open.focus();
    });
    video.addEventListener("error", () => {
        error.hidden = false;
    });
    video.addEventListener("loadedmetadata", resize);
    video.addEventListener("resize", resize);
    window.addEventListener("resize", () => {
        if (dialog.open) resize();
    });
});
