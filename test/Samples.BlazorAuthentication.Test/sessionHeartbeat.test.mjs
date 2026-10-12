// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { setImmediate } from "node:timers/promises";
import test from "node:test";

// Load the browser module directly without adding a package manifest or browser-test dependency.
const source = await readFile(new URL("../../samples/Samples.BlazorAuthentication/wwwroot/sessionHeartbeat.js", import.meta.url), "utf8");
const { start } = await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);

function browser(t) {
    // Control time, visibility, fetch and callbacks so idle/active transitions can be tested
    // deterministically, without waiting for real heartbeat intervals or renewing real sessions.
    let now = 1_000_000;
    let tick;
    let response = { ok: true, text: async () => "protected-receipt" };
    const events = new Map();
    const requests = [];
    const callbacks = [];
    const errors = [];
    let cleared = false;
    const document = {
        hidden: false,
        querySelector: () => ({ content: "antiforgery-token" }),
        addEventListener: (event, listener) => events.set(event, listener),
        removeEventListener: (event) => events.delete(event)
    };
    t.mock.method(Date, "now", () => now);
    t.mock.method(console, "error", (...args) => errors.push(args));
    t.mock.method(globalThis, "setInterval", (callback, delay) => {
        assert.equal(delay, 60_000);
        tick = callback;
        return 42;
    });
    t.mock.method(globalThis, "clearInterval", (id) => {
        assert.equal(id, 42);
        cleared = true;
    });
    t.mock.method(globalThis, "fetch", async (url, options) => {
        requests.push({ url, options });
        if (response instanceof Error) throw response;
        return response;
    });
    const previous = globalThis.document;
    globalThis.document = document;
    const heartbeat = start({
        invokeMethodAsync: async (...args) => {
            callbacks.push(args);
            return true;
        }
    });
    t.after(() => {
        heartbeat.dispose();
        if (previous === undefined) delete globalThis.document;
        else globalThis.document = previous;
    });
    return {
        document, events, requests, callbacks, errors, heartbeat,
        advance: (milliseconds) => now += milliseconds,
        tick: () => tick(),
        respond: (value) => response = value,
        cleared: () => cleared
    };
}

test("active input renews without navigation, while idle and hidden tabs stop renewing", async (t) => {
    const b = browser(t);
    await setImmediate();
    assert.equal(b.requests.length, 1);
    assert.equal(b.requests[0].url, "/session/renew");
    assert.equal(b.requests[0].options.method, "POST");
    assert.equal(b.requests[0].options.credentials, "same-origin");
    assert.equal(b.requests[0].options.headers.RequestVerificationToken, "antiforgery-token");
    assert.deepEqual(b.callbacks[0], ["RenewalSucceeded", "protected-receipt"]);
    b.events.get("keydown")();
    await setImmediate();
    assert.equal(b.requests.length, 1);
    b.advance(120_000);
    b.tick();
    await setImmediate();
    assert.equal(b.requests.length, 1);
    b.events.get("wheel")();
    await setImmediate();
    assert.equal(b.requests.length, 2);
    b.document.hidden = true;
    b.advance(60_000);
    b.events.get("pointerdown")();
    b.tick();
    await setImmediate();
    assert.equal(b.requests.length, 2);
    b.document.hidden = false;
    b.events.get("visibilitychange")();
    await setImmediate();
    assert.equal(b.requests.length, 3);
    b.heartbeat.dispose();
    assert.equal(b.events.size, 0);
    assert.ok(b.cleared());
    assert.ok(b.requests[0].options.signal.aborted);
});

test("transient failures warn and retry on activity", async (t) => {
    const b = browser(t);
    await setImmediate();
    b.respond(new Error("offline"));
    b.advance(60_000);
    b.events.get("keydown")();
    await setImmediate();
    assert.deepEqual(b.callbacks.at(-1), ["RenewalFailed", false]);
    assert.equal(b.errors.length, 1);
    b.respond({ ok: true, text: async () => "renewed" });
    b.advance(60_000);
    b.events.get("keydown")();
    await setImmediate();
    assert.deepEqual(b.callbacks.at(-1), ["RenewalSucceeded", "renewed"]);
});

test("returning to a long-idle tab immediately checks session validity", async (t) => {
    const b = browser(t);
    await setImmediate();
    b.document.hidden = true;
    b.advance(3_600_000);
    b.tick();
    await setImmediate();
    assert.equal(b.requests.length, 1);
    b.respond({ ok: false, status: 401 });
    b.document.hidden = false;
    b.events.get("visibilitychange")();
    await setImmediate();
    assert.equal(b.requests.length, 2);
    assert.deepEqual(b.callbacks.at(-1), ["RenewalFailed", true]);
});

for (const status of [400, 401, 403]) {
    test(`HTTP ${status} stops renewal instead of reviving a failed session`, async (t) => {
        const b = browser(t);
        await setImmediate();
        b.respond({ ok: false, status });
        b.advance(60_000);
        b.events.get("keydown")();
        await setImmediate();
        assert.deepEqual(b.callbacks.at(-1), ["RenewalFailed", true]);
        const count = b.requests.length;
        b.advance(60_000);
        b.events.get("keydown")();
        b.tick();
        await setImmediate();
        assert.equal(b.requests.length, count);
    });
}
