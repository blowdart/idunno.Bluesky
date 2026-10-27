// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

(() => {
    "use strict";
    const key = "xenon.theme";
    const root = document.documentElement;
    const valid = value => value === "light" || value === "dark";
    let preference = "system";
    let storageError = false;
    try {
        const saved = localStorage.getItem(key);
        if (valid(saved)) preference = saved;
    } catch (error) {
        storageError = true;
        console.warn("Xenon could not read the saved theme preference.", error);
    }

    const apply = value => {
        preference = valid(value) ? value : "system";
        if (preference === "system") root.removeAttribute("data-theme");
        else root.setAttribute("data-theme", preference);
    };
    apply(preference);

    document.addEventListener("DOMContentLoaded", () => {
        const control = document.getElementById("theme-choice");
        const status = document.getElementById("theme-status");
        if (!control || !status) return;
        const showStorageError = () => {
            status.textContent = "Your browser could not save or read the theme preference. Changes apply to this page only.";
        };
        control.value = preference;
        control.disabled = false;
        if (storageError) showStorageError();
        control.addEventListener("change", () => {
            apply(control.value);
            try {
                if (preference === "system") localStorage.removeItem(key);
                else localStorage.setItem(key, preference);
                status.textContent = "";
            } catch (error) {
                console.warn("Xenon could not save the theme preference.", error);
                showStorageError();
            }
        });
        window.addEventListener("storage", event => {
            if (event.storageArea === localStorage && (event.key === key || event.key === null)) {
                apply(event.newValue);
                control.value = preference;
                status.textContent = "";
            }
        });
    });
})();
