// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

// Delegate clicks so image buttons also work after interactive navigation or timeline refresh.
// Opening the native dialog locally avoids a circuit round trip and gives us focus trapping,
// Escape-to-close and focus restoration. The dialog's form supplies the visible Close button.
document.addEventListener("click", event => {
    const button = event.target.closest?.("button[data-open-image]");
    if (!button) return;

    const dialog = button.closest(".post-image-viewer").querySelector("dialog");
    if (!dialog.open) dialog.showModal();
});
