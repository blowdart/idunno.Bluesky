// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

(() => {
    "use strict";
    const queue = document.querySelector("[data-live-queue]");
    const status = document.querySelector("[data-queue-update-status]");
    if (!queue || !status) return;
    let timer;
    let controller;
    let stopped = false;
    let suspended = false;

    function showStatus(message) {
        status.textContent = message;
        status.hidden = !message;
    }

    async function refresh() {
        if (stopped || suspended || document.visibilityState !== "visible" || controller) return;
        clearTimeout(timer);
        controller = new AbortController();
        const deadline = setTimeout(() => controller.abort(), 10000);
        try {
            const response = await fetch(window.location.href, {
                credentials: "same-origin",
                cache: "no-store",
                redirect: "manual",
                headers: { Accept: "text/html" },
                signal: controller.signal
            });
            if (response.status === 401 || response.status === 403 || response.type === "opaqueredirect") {
                stopped = true;
                queue.replaceChildren();
                showStatus("Queue updates stopped. Reload the page to check your sign-in and team access.");
                return;
            }
            if (!response.ok) throw new Error("Queue request failed.");
            const page = new DOMParser().parseFromString(await response.text(), "text/html");
            const updated = page.querySelector("[data-live-queue]");
            if (!updated || updated.querySelector('[role="alert"]') || page.querySelector('[role="alert"]')) {
                throw new Error("Queue response is unavailable.");
            }
            // Do not remove a focused link or an expanded details panel while someone is reading it.
            if (!queue.contains(document.activeElement) && !queue.querySelector("details[open]")) {
                queue.replaceChildren(...Array.from(updated.childNodes, node => document.importNode(node, true)));
                document.dispatchEvent(new Event("xenon:queue-updated"));
            }
            showStatus("");
        } catch (error) {
            if (!suspended && document.visibilityState === "visible") {
                showStatus("Queues could not be refreshed. Displayed results may be outdated.");
            }
        } finally {
            clearTimeout(deadline);
            controller = null;
            if (!stopped && !suspended && document.visibilityState === "visible") {
                timer = setTimeout(refresh, 30000);
            }
        }
    }

    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") refresh();
        else {
            clearTimeout(timer);
            controller?.abort();
        }
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
    timer = setTimeout(refresh, 30000);
})();
