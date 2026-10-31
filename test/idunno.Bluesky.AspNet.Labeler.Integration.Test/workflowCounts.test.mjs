// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";

const script = readFileSync(new URL("../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/workflow-counts.js", import.meta.url), "utf8");

function start(fetch) {
    const links = Object.fromEntries(["cases", "reports", "appeals"].map(field => [field, { textContent: field }]));
    const status = { textContent: "", hidden: true };
    const navigation = {
        dataset: { pageTitle: "Cases · Xenon", countsUrl: "/Xenon?handler=Counts" },
        querySelector: selector => links[selector.match(/"(\w+)"/)[1]]
    };
    const events = {};
    const timers = new Map();
    let nextTimer = 0;
    const document = {
        title: "C:1 R:1 A:1 · Cases · Xenon",
        visibilityState: "hidden",
        querySelector: selector => selector === "[data-workflow-counts]" ? navigation : status,
        addEventListener: (event, handler) => { events[event] = handler; }
    };
    runInNewContext(script, {
        document,
        window: { addEventListener: (event, handler) => { events[event] = handler; } },
        fetch,
        AbortController,
        setTimeout: (handler, delay) => { timers.set(++nextTimer, { handler, delay }); return nextTimer; },
        clearTimeout: id => timers.delete(id)
    });
    return { document, links, status, events, timers };
}

// Flushes promise continuations without advancing the simulated polling clock.
async function settle() {
    for (let i = 0; i < 10; i++) await Promise.resolve();
}

test("refreshes titles and navigation even in a background tab and schedules another poll", async () => {
    let calls = 0;
    const state = start(async (url, options) => {
        calls++;
        assert.equal(url, "/Xenon?handler=Counts");
        assert.equal(options.cache, "no-store");
        assert.equal(options.redirect, "manual");
        return { ok: true, json: async () => ({ cases: calls * 3, reports: 4, appeals: 2 }) };
    });
    await settle();
    assert.equal(state.document.title, "C:3 R:4 A:2 · Cases · Xenon");
    assert.equal(state.links.cases.textContent, "Cases (3)");
    assert.equal(state.status.hidden, true);
    const poll = [...state.timers.values()].find(timer => timer.delay === 30000);
    assert.ok(poll);
    poll.handler();
    await settle();
    assert.equal(state.document.title, "C:6 R:4 A:2 · Cases · Xenon");
    state.document.visibilityState = "visible";
    state.events.visibilitychange();
    await settle();
    assert.equal(calls, 3);
});

test("failed and invalid refreshes preserve counts, mark them stale, and recover", async () => {
    let calls = 0;
    const state = start(async () => {
        calls++;
        if (calls === 1) throw new Error("Offline");
        return { ok: true, json: async () => calls === 2 ? { cases: -1 } : { cases: 0, reports: 0, appeals: 0 } };
    });
    await settle();
    assert.match(state.document.title, /Counts stale$/);
    assert.equal(state.status.hidden, false);
    state.document.visibilityState = "visible";
    state.events.visibilitychange();
    await settle();
    assert.match(state.document.title, /Counts stale$/);
    state.events.visibilitychange();
    await settle();
    assert.equal(state.document.title, "C:0 R:0 A:0 · Cases · Xenon");
    assert.equal(state.status.hidden, true);
});

for (const response of [{ status: 401 }, { status: 403 }, { type: "opaqueredirect" }]) {
    test(`stops after lost authorization: ${JSON.stringify(response)}`, async () => {
        let calls = 0;
        const state = start(async () => { calls++; return response; });
        await settle();
        assert.equal(state.document.title, "Cases · Xenon");
        assert.equal(state.links.cases.textContent, "Cases");
        assert.match(state.status.textContent, /stopped/);
        assert.equal(state.timers.size, 0);
        state.document.visibilityState = "visible";
        state.events.visibilitychange();
        await settle();
        assert.equal(calls, 1);
    });
}

test("does not overlap requests and aborts a request when leaving the page", async () => {
    let calls = 0;
    let release;
    let signal;
    const state = start((url, options) => {
        calls++;
        signal = options.signal;
        return new Promise(resolve => { release = resolve; });
    });
    state.document.visibilityState = "visible";
    state.events.visibilitychange();
    assert.equal(calls, 1);
    state.events.pagehide();
    assert.equal(signal.aborted, true);
    release({ ok: true, json: async () => ({ cases: 1, reports: 2, appeals: 3 }) });
    await settle();
    assert.equal(state.timers.size, 0);
    state.events.pageshow();
    assert.equal(calls, 2);
});
