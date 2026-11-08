// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/queue-updates.js"), "utf8");

function page() {
    const listeners = new Map();
    const timers = new Map();
    let sequence = 0;
    let focused = false;
    let expanded = false;
    let parsed = true;
    let failed = false;
    let code = 200;
    let redirect = false;
    let hold;
    const calls = [];
    const imported = [];
    const status = { hidden: true, textContent: "" };
    const queue = {
        children: ["old"],
        contains() { return focused; },
        querySelector() { return expanded; },
        replaceChildren(...nodes) { this.children = nodes; }
    };
    const document = {
        visibilityState: "visible",
        activeElement: {},
        querySelector(selector) { return selector === "[data-live-queue]" ? queue : status; },
        addEventListener(name, callback) { listeners.set(name, callback); },
        importNode(node) { imported.push(node); return node; },
        dispatchEvent(event) { assert.equal(event.type, "xenon:queue-updated"); }
    };
    runInNewContext(script, {
        document,
        window: {
            location: { href: "https://example.test/Xenon/Reports?Queue=InReview&Sort=Number&Ascending=true&Page=3" },
            addEventListener(name, callback) { listeners.set(name, callback); }
        },
        setTimeout(callback, milliseconds) { const id = ++sequence; timers.set(id, { callback, milliseconds }); return id; },
        clearTimeout(id) { timers.delete(id); },
        AbortController, Event,
        DOMParser: class {
            parseFromString() {
                return {
                    querySelector(selector) {
                        if (selector !== "[data-live-queue]") return null;
                        return parsed ? { childNodes: ["new"], querySelector() { return null; } } : null;
                    }
                };
            }
        },
        async fetch(url, options) {
            calls.push({ url, options });
            if (hold) await hold;
            if (failed) throw new Error("Network failure");
            return { status: code, ok: code === 200, type: redirect ? "opaqueredirect" : "basic", async text() { return "html"; } };
        }
    });
    return {
        queue, status, calls, imported, document, timers,
        async tick() {
            const entry = [...timers].find(([, value]) => value.milliseconds === 30000);
            assert.ok(entry);
            timers.delete(entry[0]);
            await entry[1].callback();
        },
        async event(name) { await listeners.get(name)(); await new Promise(resolve => setImmediate(resolve)); },
        configure(values) {
            focused = values.focused ?? focused;
            expanded = values.expanded ?? expanded;
            parsed = values.parsed ?? parsed;
            failed = values.failed ?? failed;
            code = values.code ?? code;
            redirect = values.redirect ?? redirect;
            hold = values.hold ?? hold;
        }
    };
}

test("Refresh is read-only, preserves the full submitted filter URL, and replaces results only", async () => {
    const view = page();
    await view.tick();
    assert.equal(view.calls[0].url, "https://example.test/Xenon/Reports?Queue=InReview&Sort=Number&Ascending=true&Page=3");
    assert.equal(view.calls[0].options.credentials, "same-origin");
    assert.equal(view.calls[0].options.cache, "no-store");
    assert.equal(view.calls[0].options.redirect, "manual");
    assert.equal(view.calls[0].options.body, undefined);
    assert.deepEqual(view.imported, ["new"]);
    assert.equal(view.status.hidden, true);
    assert.ok([...view.timers.values()].some(timer => timer.milliseconds === 30000));
});

for (const option of ["focused", "expanded"]) {
    test(`Refresh does not displace ${option} results`, async () => {
        const view = page();
        view.configure({ [option]: true });
        await view.tick();
        assert.deepEqual(view.queue.children, ["old"]);
        view.configure({ [option]: false });
        await view.tick();
        assert.equal(view.imported.length, 1);
    });
}

for (const configuration of [{ failed: true }, { parsed: false }, { code: 503 }]) {
    test(`Failed refresh retains results with an explicit stale notice: ${JSON.stringify(configuration)}`, async () => {
        const view = page();
        view.configure(configuration);
        await view.tick();
        assert.deepEqual(view.queue.children, ["old"]);
        assert.match(view.status.textContent, /outdated/);
        assert.equal(view.status.hidden, false);
    });
}

for (const configuration of [{ code: 401 }, { code: 403 }, { redirect: true }]) {
    test(`Revoked access clears results and stops polling: ${JSON.stringify(configuration)}`, async () => {
        const view = page();
        view.configure(configuration);
        await view.tick();
        assert.equal(view.queue.children.length, 0);
        assert.match(view.status.textContent, /sign-in and team access/);
        assert.equal(view.timers.size, 0);
        await view.event("pageshow");
        assert.equal(view.calls.length, 1);
    });
}

test("Hidden and page-hidden documents stop polling and resume when visible", async () => {
    const view = page();
    view.document.visibilityState = "hidden";
    await view.event("visibilitychange");
    assert.equal(view.timers.size, 0);
    assert.equal(view.calls.length, 0);
    view.document.visibilityState = "visible";
    await view.event("visibilitychange");
    assert.equal(view.calls.length, 1);
    await view.event("pagehide");
    assert.equal(view.timers.size, 0);
    await view.event("pageshow");
    assert.equal(view.calls.length, 2);
});

test("In-flight refreshes never overlap", async () => {
    const view = page();
    let complete;
    view.configure({ hold: new Promise(resolve => { complete = resolve; }) });
    const pending = view.tick();
    await view.event("pageshow");
    assert.equal(view.calls.length, 1);
    complete();
    await pending;
    assert.equal(view.imported.length, 1);
});
