// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

document.querySelectorAll("[data-team-change]").forEach(form => {
    const action = form.querySelector('select[name="action"]');
    const role = form.querySelector("[data-team-role]");
    const member = form.querySelector("[data-team-member]");
    const updateMemberRole = () => {
        const handle = member.value.trim().replace(/^@/, "").toLowerCase();
        const selectedRole = Array.from(member.list.options)
            .find(option => option.value.toLowerCase() === handle)?.dataset.role;
        if (selectedRole) role.querySelector("select").value = selectedRole;
    };
    member.addEventListener("input", updateMemberRole);
    member.addEventListener("change", updateMemberRole);
    const update = () => { role.hidden = action.value !== "SetRole"; };
    action.addEventListener("change", update);
    update();
});

document.querySelectorAll("[data-handle-typeahead]").forEach(container => {
    const input = container.querySelector("input");
    const list = container.querySelector('[role="listbox"]');
    const status = container.querySelector('[role="status"]');
    let timer;
    let controller;
    let revision = 0;
    let actors = [];
    let active = -1;

    input.setAttribute("role", "combobox");
    input.setAttribute("aria-autocomplete", "list");
    input.setAttribute("aria-controls", list.id);
    input.setAttribute("aria-expanded", "false");

    const close = () => {
        clearTimeout(timer);
        controller?.abort();
        revision++;
        actors = [];
        active = -1;
        list.replaceChildren();
        list.hidden = true;
        input.setAttribute("aria-expanded", "false");
        input.removeAttribute("aria-activedescendant");
    };

    const select = actor => {
        close();
        input.value = actor.handle;
        status.classList.add("sr-only");
        status.textContent = `Selected @${actor.handle}.`;
        input.focus();
    };

    const highlight = index => {
        active = index;
        Array.from(list.children).forEach((option, position) => {
            option.setAttribute("aria-selected", String(position === active));
        });
        const option = list.children[active];
        input.setAttribute("aria-activedescendant", option.id);
        option.scrollIntoView({ block: "nearest" });
    };

    const search = async (query, expectedRevision) => {
        controller = new AbortController();
        const request = controller;
        const timeout = setTimeout(() => request.abort(), 8000);
        status.textContent = "Looking for accounts...";
        try {
            const url = new URL("https://public.api.bsky.app/xrpc/app.bsky.actor.searchActorsTypeahead");
            url.searchParams.set("q", query);
            url.searchParams.set("limit", "8");
            const response = await fetch(url, {
                credentials: "omit",
                referrerPolicy: "no-referrer",
                signal: request.signal
            });
            if (!response.ok) throw new Error("Account search failed.");
            const data = await response.json();
            if (!Array.isArray(data.actors)) throw new Error("Invalid account search response.");
            if (revision !== expectedRevision) return;

            actors = data.actors.filter(actor =>
                actor && typeof actor.did === "string" && /^did:[a-z]+:\S+$/.test(actor.did) &&
                actor.did.length <= input.maxLength &&
                typeof actor.handle === "string" && actor.handle.length <= 253 &&
                /^[a-zA-Z0-9.-]+$/.test(actor.handle) && actor.handle !== "handle.invalid").slice(0, 8);
            actors.forEach((actor, index) => {
                const option = document.createElement("li");
                option.id = `${list.id}-${index}`;
                option.setAttribute("role", "option");
                option.setAttribute("aria-selected", "false");
                const handle = document.createElement("strong");
                handle.textContent = `@${actor.handle}`;
                const name = document.createElement("span");
                name.textContent = typeof actor.displayName === "string" ? actor.displayName.slice(0, 256) : "";
                option.append(handle, name);
                // Keep focus on the combobox until the pointer selection is committed.
                option.addEventListener("pointerdown", event => event.preventDefault());
                option.addEventListener("click", () => select(actor));
                list.append(option);
            });
            list.hidden = actors.length === 0;
            input.setAttribute("aria-expanded", String(!list.hidden));
            status.textContent = actors.length
                ? `${actors.length} suggestions. Use the up and down arrow keys, then Enter to select.`
                : "No matching accounts. You can still enter a handle directly.";
        } catch {
            if (revision !== expectedRevision) return;
            status.textContent = "Account suggestions are unavailable. You can still enter a handle directly.";
        } finally {
            clearTimeout(timeout);
        }
    };

    const queueSearch = () => {
        close();
        status.classList.remove("sr-only");
        status.textContent = "";
        const query = input.value.trim().replace(/^@/, "");
        if (query.length < 2 || query.length > 253 || query.startsWith("did:")) return;
        const expectedRevision = revision;
        timer = setTimeout(() => search(query, expectedRevision), 300);
    };

    input.addEventListener("input", event => {
        if (event.isComposing) {
            close();
            status.textContent = "";
            return;
        }
        queueSearch();
    });
    input.addEventListener("compositionend", queueSearch);
    input.addEventListener("keydown", event => {
        if (event.isComposing) return;
        if (event.key === "Escape") {
            close();
            status.textContent = "";
        } else if (actors.length && (event.key === "ArrowDown" || event.key === "ArrowUp")) {
            event.preventDefault();
            highlight(event.key === "ArrowDown"
                ? (active + 1) % actors.length
                : (active < 0 ? actors.length - 1 : (active - 1 + actors.length) % actors.length));
        } else if (event.key === "Enter" && active >= 0) {
            event.preventDefault();
            select(actors[active]);
        }
    });
    input.addEventListener("blur", () => {
        close();
        status.textContent = "";
    });
    input.form.addEventListener("submit", close);
});
