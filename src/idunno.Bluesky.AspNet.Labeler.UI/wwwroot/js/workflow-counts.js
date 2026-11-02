// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

(() => {
    "use strict";
    const navigation = document.querySelector("[data-workflow-counts]");
    if (!navigation) return;
    const status = document.querySelector("[data-workflow-count-status]");
    const baseTitle = navigation.dataset.pageTitle;
    const fields = [["cases", "Cases"], ["reports", "Reports"], ["appeals", "Appeals"]];
    let timer;
    let controller;
    let stopped = false;
    let suspended = false;

    function showStatus(message) {
        status.textContent = message;
        status.hidden = !message;
    }

    async function refresh() {
        if (stopped || suspended || controller) return;
        clearTimeout(timer);
        controller = new AbortController();
        const deadline = setTimeout(() => controller.abort(), 10000);
        try {
            const response = await fetch(navigation.dataset.countsUrl, {
                credentials: "same-origin",
                cache: "no-store",
                redirect: "manual",
                headers: { Accept: "application/json" },
                signal: controller.signal
            });
            if (response.status === 401 || response.status === 403 || response.type === "opaqueredirect") {
                stopped = true;
                document.title = baseTitle;
                for (const [field, label] of fields) {
                    navigation.querySelector(`[data-workflow-count="${field}"]`).textContent = label;
                }
                showStatus("Workflow count updates stopped. Reload the page to check your sign-in and team access.");
                return;
            }
            if (!response.ok) throw new Error("Workflow count request failed.");
            const counts = await response.json();
            if (!counts || !fields.every(([field]) => Number.isSafeInteger(counts[field]) && counts[field] >= 0)) {
                throw new Error("Invalid workflow counts.");
            }
            for (const [field, label] of fields) {
                navigation.querySelector(`[data-workflow-count="${field}"]`).textContent = `${label} (${counts[field]})`;
            }
            document.title = `C:${counts.cases} R:${counts.reports} A:${counts.appeals} · ${baseTitle}`;
            showStatus("");
        } catch (error) {
            if (!suspended) {
                showStatus("Workflow counts could not be refreshed. Displayed counts may be outdated.");
                document.title = `${document.title.replace(/ · Counts stale$/, "")} · Counts stale`;
            }
        } finally {
            clearTimeout(deadline);
            controller = null;
            if (!stopped && !suspended) timer = setTimeout(refresh, 30000);
        }
    }

    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") refresh();
    });
    window.addEventListener("pagehide", () => {
        suspended = true;
        clearTimeout(timer);
        controller?.abort();
    });
    window.addEventListener("pageshow", () => {
        suspended = false;
        refresh();
    });
    refresh();
})();
