// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

(() => {
    "use strict";
    document.querySelectorAll("[data-manual-labels]").forEach(form => {
        const target = form.elements.namedItem("target");
        const cid = form.elements.namedItem("cid");
        const cidField = form.querySelector("[data-record-cid]");
        const reason = form.elements.namedItem("reason");
        const controls = form.querySelector("[data-manual-label-controls]");
        const load = form.querySelector("[data-load-labels]");
        const submit = form.querySelector("[data-submit-labels]");
        let timer;
        let pending;
        let generation = 0;

        function updateTarget() {
            const value = target.value.trim();
            const record = (value.startsWith("at://") && !/^at:\/\/[^/?#]+$/.test(value)) ||
                /^https:\/\/bsky\.app\/(?:profile\/[^/?#]+\/(?:post|lists|feed)\/|starter-pack\/[^/?#]+\/)/i.test(value);
            cidField.hidden = !record;
            cid.disabled = !record;
            if (!record) cid.value = "";
        }

        updateTarget();
        load.hidden = !!controls.querySelector('[name="labelState"]').value ||
            !controls.querySelector('[role="alert"]');

        function invalidate() {
            clearTimeout(timer);
            generation++;
            pending?.abort();
            submit.disabled = true;
            submit.textContent = "Submit label changes";
            reason.readOnly = false;
            controls.querySelector('[name="labelState"]').value = "";
            controls.querySelector("fieldset").disabled = true;
            controls.querySelector("fieldset").hidden = true;
            controls.setAttribute("aria-busy", "false");
            load.hidden = true;
        }

        async function lookup() {
            invalidate();
            if (!target.value.trim()) return;
            const current = generation;
            pending = new AbortController();
            controls.setAttribute("aria-busy", "true");
            try {
                const response = await fetch(load.formAction, {
                    method: "POST",
                    body: new FormData(form),
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    signal: pending.signal
                });
                if (!response.ok || response.redirected) throw new Error("Label lookup failed.");
                const html = await response.text();
                if (current !== generation) return;
                // This is the authenticated, same-origin Razor partial; never remote subject markup.
                const template = document.createElement("template");
                template.innerHTML = html;
                if (!template.content.querySelector('[name="labelState"]')) throw new Error("Invalid label response.");
                controls.replaceChildren(template.content);
                const state = controls.querySelector('[name="labelState"]');
                submit.disabled = !state.value;
                if (!submit.disabled && state.dataset.target) {
                    target.value = state.dataset.target;
                    updateTarget();
                }
                load.hidden = !submit.disabled;
            } catch (error) {
                if (current !== generation || error.name === "AbortError") return;
                const message = document.createElement("p");
                message.setAttribute("role", "alert");
                message.textContent = "Labels could not be loaded. Load labels again before submitting changes.";
                controls.append(message);
                load.hidden = false;
            } finally {
                if (current === generation) controls.setAttribute("aria-busy", "false");
            }
        }

        function changed() {
            invalidate();
            timer = setTimeout(lookup, 400);
        }

        target.addEventListener("input", () => {
            updateTarget();
            changed();
        });
        cid.addEventListener("input", changed);
        load.addEventListener("click", event => {
            event.preventDefault();
            return lookup();
        });
        form.addEventListener("submit", event => {
            if (submit.disabled) event.preventDefault();
        });
    });
})();
