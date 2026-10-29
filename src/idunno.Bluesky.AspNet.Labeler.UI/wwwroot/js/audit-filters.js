// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

(() => {
    "use strict";
    document.querySelectorAll("[data-audit-filters]").forEach(form => {
        const local = form.querySelector("[data-audit-local]");
        const zone = form.querySelector("[data-audit-zone]");
        const name = form.querySelector("[data-audit-zone-name]");
        const tooltip = form.querySelector("[data-audit-zone-tooltip]");
        if (!local || !zone || !name || !tooltip) return;
        try {
            const timeZone = zone.value || Intl.DateTimeFormat().resolvedOptions().timeZone;
            if (!timeZone) throw new Error("The browser did not provide a time zone.");
            new Intl.DateTimeFormat(undefined, { timeZone });
            zone.value = timeZone;
            name.title = timeZone;
            tooltip.textContent = timeZone;
            local.disabled = false;
        } catch (error) {
            name.title = "Local time zone unavailable; use UTC.";
            tooltip.textContent = name.title;
            console.warn("Xenon could not identify the audit filter time zone.", error);
        }
    });
})();
