// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

document.querySelectorAll('[data-media-warning="true"]').forEach(preview => {
    const buttons = preview.querySelectorAll("[data-media-toggle]");
    const images = preview.querySelectorAll("[data-sensitive-image]");
    const toggle = () => {
        const blurred = preview.classList.toggle("media-blurred");
        buttons.forEach(button => {
            const label = blurred ? "Show images" : "Blur images";
            button.setAttribute("aria-label", label);
            button.setAttribute("title", label);
            button.setAttribute("aria-pressed", String(!blurred));
            const icon = button.querySelector("[data-media-icon]");
            icon.classList.toggle("fa-eye", blurred);
            icon.classList.toggle("fa-eye-slash", !blurred);
        });
        images.forEach(image => {
            if (blurred) image.setAttribute("aria-hidden", "true");
            else image.removeAttribute("aria-hidden");
        });
    };
    buttons.forEach(button => {
        button.addEventListener("click", toggle);
        button.hidden = false;
    });
});
