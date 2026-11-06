// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

(() => {
    "use strict";
    const menu = document.querySelector("[data-online-presence]");
    if (!menu) return;
    const count = menu.querySelector("[data-online-count]");
    const members = menu.querySelector("[data-online-members]");
    const status = menu.querySelector("[data-online-status]");
    let timer;
    let controller;
    let stopped = false;
    let suspended = false;

    async function refresh() {
        if (stopped || suspended || controller || document.visibilityState === "hidden") return;
        clearTimeout(timer);
        controller = new AbortController();
        const deadline = setTimeout(() => controller.abort(), 10000);
        try {
            const response = await fetch(menu.dataset.onlineUrl, {
                credentials: "same-origin",
                cache: "no-store",
                redirect: "manual",
                headers: { Accept: "application/json" },
                signal: controller.signal
            });
            if (response.status === 204 || response.status === 401 || response.status === 403 || response.type === "opaqueredirect") {
                stopped = true;
                menu.hidden = true;
                menu.removeAttribute("open");
                members.replaceChildren();
                return;
            }
            if (!response.ok) throw new Error("Online activity request failed.");
            const body = await response.json();
            if (!body || !Array.isArray(body.members) ||
                !body.members.every(member => typeof member.did === "string" && member.did.startsWith("did:") &&
                    typeof member.displayName === "string" && member.displayName.length > 0 &&
                    typeof member.lastActive === "string" && Number.isFinite(Date.parse(member.lastActive)))) {
                throw new Error("Invalid online activity response.");
            }
            members.replaceChildren(...body.members.map(member => {
                const item = document.createElement("li");
                const icon = document.createElement("span");
                icon.className = "fa-icon fa-user";
                icon.setAttribute("aria-hidden", "true");
                const handle = document.createElement("span");
                handle.textContent = member.displayName;
                item.append(icon, handle);
                item.title = member.did;
                return item;
            }));
            count.textContent = `${body.members.length} online`;
            status.textContent = "";
            status.hidden = true;
        } catch (error) {
            if (!suspended && document.visibilityState !== "hidden") {
                count.textContent = "Online status unavailable";
                members.replaceChildren();
                status.textContent = "Online activity could not be refreshed.";
                status.hidden = false;
            }
        } finally {
            clearTimeout(deadline);
            controller = null;
            if (!stopped && !suspended) timer = setTimeout(refresh, 30000);
        }
    }

    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "hidden") {
            clearTimeout(timer);
            controller?.abort();
        } else {
            refresh();
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
    refresh();
})();
