// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/manual-labels.js"), "utf8");

function page() {
    const callbacks = {};
    const timers = new Map();
    const requests = [];
    const fields = Object.fromEntries(["target", "cid", "reason"].map(name => [name, {
        value: "",
        readOnly: false,
        addEventListener(event, callback) { callbacks[name] = callback; }
    }]));
    const state = { value: "old-state", dataset: {} };
    const fieldset = { disabled: false, hidden: false };
    const alerts = [];
    const cidField = { hidden: false };
    const attributes = {};
    const controls = {
        querySelector(selector) {
            if (selector === '[role="alert"]') return alerts[0] ?? null;
            return selector === "fieldset" ? fieldset : state;
        },
        setAttribute(name, value) { attributes[name] = value; },
        replaceChildren(content) {
            const [value, target, cid] = content.state.split("|");
            state.value = value;
            state.dataset.target = target;
            state.dataset.cid = cid;
            fieldset.disabled = !value;
            fieldset.hidden = false;
        },
        append(message) { alerts.push(message); }
    };
    const submit = { disabled: false, textContent: "Submit label changes" };
    const load = {
        formAction: "/Xenon/Labels?handler=Lookup",
        addEventListener(event, callback) { callbacks.load = callback; }
    };
    const currentCid = {
        formAction: "/Xenon/Labels?handler=CurrentCid",
        addEventListener(event, callback) { callbacks.currentCid = callback; }
    };
    const duration = { disabled: false, value: "24" };
    const frozenDuration = { removed: false, remove() { this.removed = true; } };
    const form = {
        elements: { namedItem(name) { return fields[name]; } },
        querySelector(selector) {
            return {
                "[data-manual-label-controls]": controls,
                "[data-load-labels]": load,
                "[data-current-cid]": currentCid,
                "[data-record-cid]": cidField,
                "[data-submit-labels]": submit,
                'select[name="labelDurationHours"]': duration,
                'input[type="hidden"][name="labelDurationHours"]': frozenDuration
            }[selector];
        },
        addEventListener(event, callback) { callbacks.submit = callback; }
    };
    runInNewContext(script, {
        AbortController,
        FormData: class {
            constructor() { this.target = fields.target.value; this.cid = fields.cid.value; }
        },
        setTimeout(callback) { const id = timers.size + 1; timers.set(id, callback); return id; },
        clearTimeout(id) { timers.delete(id); },
        fetch(url, options) {
            return new Promise(resolve => requests.push({ url, options, resolve }));
        },
        document: {
            querySelectorAll() { return [form]; },
            createElement(tag) {
                if (tag === "template") {
                    return {
                        set innerHTML(html) { this.content = { state: html, querySelector() { return html !== "invalid"; } }; }
                    };
                }
                return { setAttribute(name, value) { this[name] = value; }, textContent: "" };
            }
        }
    });
    const event = () => ({ prevented: false, preventDefault() { this.prevented = true; } });
    return {
        fields, callbacks, requests, state, fieldset, submit, loadButton: load, currentCid, cidField, alerts, attributes, event, duration, frozenDuration,
        load() { return callbacks.load(event()); },
        fireTimer() { const callback = [...timers.values()].at(-1); timers.clear(); return callback(); }
    };
}

test("Explicit CID lookup fills the current version and reloads the protected baseline without issuing labels", async () => {
    const p = page();
    p.fields.target.value = "at://did:plc:example/app.bsky.feed.post/one";
    p.callbacks.target();
    const loading = p.callbacks.currentCid(p.event());
    assert.equal(p.requests[0].url, "/Xenon/Labels?handler=CurrentCid");
    complete(p.requests[0], "pinned-state|at://did:plc:example/app.bsky.feed.post/one|current-cid");
    await loading;
    assert.equal(p.fields.cid.value, "current-cid");
    assert.equal(p.state.value, "pinned-state");
    assert.equal(p.submit.disabled, false);
});

test("A stale current-CID response cannot overwrite a subsequently edited target", async () => {
    const p = page();
    p.fields.target.value = "at://did:plc:example/app.bsky.feed.post/one";
    p.callbacks.target();
    const loading = p.callbacks.currentCid(p.event());
    p.fields.target.value = "at://did:plc:example/app.bsky.feed.post/two";
    p.callbacks.target();
    complete(p.requests[0], "old-state|at://did:plc:example/app.bsky.feed.post/one|old-cid");
    await loading;
    assert.equal(p.fields.cid.value, "");
    assert.equal(p.submit.disabled, true);
});

test("A failed current-CID lookup preserves the entered CID and prevents submission", async () => {
    const p = page();
    p.fields.target.value = "at://did:plc:example/app.bsky.feed.post/one";
    p.fields.cid.value = "entered-cid";
    const loading = p.callbacks.currentCid(p.event());
    complete(p.requests[0], "|at://did:plc:example/app.bsky.feed.post/one|");
    await loading;
    assert.equal(p.fields.cid.value, "entered-cid");
    assert.equal(p.state.value, "");
    assert.equal(p.submit.disabled, true);
    assert.equal(p.loadButton.hidden, false);
});

function complete(request, body = "fresh-state", overrides = {}) {
    request.resolve({ ok: true, redirected: false, text: async () => body, ...overrides });
}

test("Target edits invalidate old selections immediately and debounce an antiforgery form POST", async () => {
    const p = page();
    p.fields.target.value = "at://did:plc:example/app.bsky.feed.post/one";
    p.callbacks.target();
    assert.equal(p.cidField.hidden, false);
    assert.equal(p.fields.cid.disabled, false);
    assert.equal(p.state.value, "");
    assert.equal(p.fieldset.hidden, true);
    assert.equal(p.submit.disabled, true);
    const blocked = p.event();
    p.callbacks.submit(blocked);
    assert.equal(blocked.prevented, true);
    assert.equal(p.requests.length, 0);
    const loading = p.fireTimer();
    assert.equal(p.requests[0].url, "/Xenon/Labels?handler=Lookup");
    assert.equal(p.requests[0].options.method, "POST");
    assert.equal(p.requests[0].options.credentials, "same-origin");
    assert.equal(p.attributes["aria-busy"], "true");
    complete(p.requests[0]);
    await loading;
    assert.equal(p.state.value, "fresh-state");
    assert.equal(p.submit.disabled, false);
    assert.equal(p.fieldset.hidden, false);
    assert.equal(p.attributes["aria-busy"], "false");
});

test("A superseded lookup cannot replace the new target's labels, even if cancellation arrives late", async () => {
    const p = page();
    p.fields.target.value = "first.example";
    const first = p.load();
    p.fields.target.value = "second.example";
    p.callbacks.target();
    assert.equal(p.requests[0].options.signal.aborted, true);
    const second = p.fireTimer();
    complete(p.requests[1], "second-state");
    await second;
    complete(p.requests[0], "first-state");
    await first;
    assert.equal(p.state.value, "second-state");
    assert.equal(p.submit.disabled, false);
});

test("Changing CID or target invalidates the loaded state and frozen retry intent", async () => {
    for (const name of ["cid", "target"]) {
        const p = page();
        p.fields.reason.readOnly = true;
        p.duration.disabled = true;
        p.callbacks[name]();
        assert.equal(p.submit.disabled, true);
        assert.equal(p.state.value, "");
        assert.equal(p.fields.reason.readOnly, false);
        assert.equal(p.duration.disabled, false);
        assert.equal(p.duration.value, "24");
        assert.equal(p.frozenDuration.removed, true);
    }
});

test("Lookup errors and sign-in redirects remain explicit failures with submission disabled", async () => {
    for (const response of [{ ok: false }, { redirected: true }, { body: "invalid" }]) {
        const p = page();
        p.fields.target.value = "did:plc:example";
        const loading = p.load();
        complete(p.requests[0], response.body ?? "ignored", response);
        await loading;
        assert.equal(p.submit.disabled, true);
        assert.equal(p.state.value, "");
        assert.equal(p.alerts.length, 1);
        assert.equal(p.alerts[0].role, "alert");
        assert.equal(p.loadButton.hidden, false);
        assert.match(p.alerts[0].textContent, /could not be loaded/);
        assert.equal(p.attributes["aria-busy"], "false");
    }
});

test("CID is available only for records and is cleared when switching back to an account", () => {
    const p = page();
    assert.equal(p.cidField.hidden, true);
    assert.equal(p.fields.cid.disabled, true);
    assert.equal(p.loadButton.hidden, true);
    p.fields.target.value = "at://did:plc:example/app.bsky.feed.post/one";
    p.callbacks.target();
    p.fields.cid.value = "record-cid";
    assert.equal(p.cidField.hidden, false);
    assert.equal(p.fields.cid.disabled, false);
    p.fields.target.value = "account.example";
    p.callbacks.target();
    assert.equal(p.cidField.hidden, true);
    assert.equal(p.fields.cid.disabled, true);
    assert.equal(p.fields.cid.value, "");
});

test("Empty targets cancel pending work without fetching or enabling a submission", async () => {
    const p = page();
    await p.load();
    assert.equal(p.requests.length, 0);
    assert.equal(p.submit.disabled, true);
});

test("Pasted Bluesky record URLs show CID and are replaced by the canonical result without a second lookup", async () => {
    const p = page();
    p.fields.target.value = "https://bsky.app/profile/account.example/post/one";
    p.callbacks.target();
    assert.equal(p.cidField.hidden, false);
    p.fields.cid.value = "pinned-cid";
    const loading = p.fireTimer();
    assert.equal(p.requests[0].options.body.cid, "pinned-cid");
    complete(p.requests[0], "fresh|at://did:plc:example/app.bsky.feed.post/one");
    await loading;
    assert.equal(p.fields.target.value, "at://did:plc:example/app.bsky.feed.post/one");
    assert.equal(p.fields.cid.value, "pinned-cid");
    assert.equal(p.requests.length, 1);
    assert.equal(p.submit.disabled, false);
});

test("Pasted profile links remain account targets and show the resolved DID", async () => {
    const p = page();
    p.fields.target.value = "https://bsky.app/profile/account.example";
    p.callbacks.target();
    assert.equal(p.cidField.hidden, true);
    const loading = p.fireTimer();
    complete(p.requests[0], "fresh|did:plc:example");
    await loading;
    assert.equal(p.fields.target.value, "did:plc:example");
    assert.equal(p.cidField.hidden, true);
});

test("Repository-only AT URIs become account DIDs without exposing the record CID field", async () => {
    const p = page();
    p.fields.target.value = "at://did:plc:example";
    p.callbacks.target();
    assert.equal(p.cidField.hidden, true);
    assert.equal(p.fields.cid.disabled, true);
    const loading = p.fireTimer();
    complete(p.requests[0], "fresh|did:plc:example");
    await loading;
    assert.equal(p.fields.target.value, "did:plc:example");
    assert.equal(p.cidField.hidden, true);
    assert.equal(p.submit.disabled, false);
});
