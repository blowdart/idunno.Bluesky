// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        document.querySelectorAll(".identity").forEach(identity => identity.classList.add("tooltip-dismissed"));
        for (const menu of document.querySelectorAll(".account-menu[open]")) {
            menu.removeAttribute("open");
            menu.querySelector("summary").focus();
        }
    }

});

document.addEventListener("click", event => {
    for (const menu of document.querySelectorAll(".account-menu[open]")) {
        if (!menu.contains(event.target)) {
            menu.removeAttribute("open");
        }
    }
});

for (const eventName of ["focusin", "pointerover"]) {
    document.addEventListener(eventName, event => {
        const identity = event.target.closest(".identity");
        if (identity && !identity.contains(event.relatedTarget)) {
            identity.classList.remove("tooltip-dismissed");
        }
    });
}
