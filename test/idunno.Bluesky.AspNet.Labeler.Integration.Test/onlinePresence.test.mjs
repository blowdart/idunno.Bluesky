// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";

const script = readFileSync(new URL("../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/online-presence.js", import.meta.url), "utf8");

function start(fetch, visible = true, configured = true) {
    const count = { textContent: "1 online" };
    const members = { children: [], replaceChildren(...children) { this.children = children; } };
    const status = { textContent: "", hidden: true };
    const menu = {
        hidden: false,
        dataset: { onlineUrl: "/Xenon?handler=Online" },
        querySelector: selector => ({ "[data-online-count]": count, "[data-online-members]": members, "[data-online-status]": status })[selector],
        removeAttribute: name => assert.equal(name, "open")
    };
    const events = {};
    const timers = new Map();
    let nextTimer = 0;
    const document = {
        visibilityState: visible ? "visible" : "hidden",
        querySelector: () => configured ? menu : null,
        createElement: tag => {
            assert.ok(tag === "li" || tag === "span");
            return {
                textContent: "",
                children: [],
                attributes: {},
                append(...children) { this.children.push(...children); },
                setAttribute(name, value) { this.attributes[name] = value; }
            };
        },
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
    return { document, menu, count, members, status, events, timers };
}

// Flushes request continuations without advancing the simulated polling clock.
async function settle() {
    for (let i = 0; i < 10; i++) await Promise.resolve();
}

function response(did = "did:plc:member") {
    return { ok: true, json: async () => ({ members: [{ did, displayName: "@member.example", lastActive: "2026-01-01T00:00:00Z" }] }) };
}

test("refreshes the online count and safely renders member text every thirty seconds", async () => {
    const state = start(async (url, options) => {
        assert.equal(url, "/Xenon?handler=Online");
        assert.equal(options.credentials, "same-origin");
        assert.equal(options.cache, "no-store");
        assert.equal(options.redirect, "manual");
        return response("did:plc:<script>");
    });
    await settle();
    assert.equal(state.count.textContent, "1 online");
    assert.equal(state.members.children[0].children[0].className, "fa-icon fa-user");
    assert.equal(state.members.children[0].children[0].attributes["aria-hidden"], "true");
    assert.equal(state.members.children[0].children[1].textContent, "@member.example");
    assert.equal(state.members.children[0].title, "did:plc:<script>");
    assert.equal(state.status.hidden, true);
    assert.ok([...state.timers.values()].some(timer => timer.delay === 30000));
});

for (const result of [{ status: 204 }, { status: 401 }, { status: 403 }, { type: "opaqueredirect" }]) {
    test(`clears and hides presence when unavailable or unauthorized: ${JSON.stringify(result)}`, async () => {
        let calls = 0;
        const state = start(async () => { calls++; return result; });
        await settle();
        assert.equal(state.menu.hidden, true);
        assert.equal(state.members.children.length, 0);
        assert.equal(state.timers.size, 0);
        state.events.visibilitychange();
        await settle();
        assert.equal(calls, 1);
    });
}

test("refresh errors and malformed data explicitly replace the count instead of showing stale activity", async () => {
    let calls = 0;
    const state = start(async () => {
        calls++;
        if (calls === 1) throw new Error("Offline");
        if (calls === 2) return { ok: true, json: async () => ({ members: [{}] }) };
        return response();
    });
    await settle();
    assert.equal(state.count.textContent, "Online status unavailable");
    assert.equal(state.status.hidden, false);
    state.events.visibilitychange();
    await settle();
    assert.equal(state.count.textContent, "Online status unavailable");
    state.events.visibilitychange();
    await settle();
    assert.equal(state.count.textContent, "1 online");
    assert.equal(state.status.hidden, true);
});

test("single-user pages start no polling and hidden pages wait until visible", async () => {
    let calls = 0;
    start(async () => { calls++; return response(); }, true, false);
    const state = start(async () => { calls++; return response(); }, false);
    await settle();
    assert.equal(calls, 0);
    state.document.visibilityState = "visible";
    state.events.visibilitychange();
    await settle();
    assert.equal(calls, 1);
});

test("does not overlap requests and aborts when hidden or navigating away", async () => {
    let calls = 0;
    let release;
    let signal;
    const state = start((url, options) => {
        calls++;
        signal = options.signal;
        return new Promise(resolve => { release = resolve; });
    });
    state.events.visibilitychange();
    assert.equal(calls, 1);
    state.document.visibilityState = "hidden";
    state.events.visibilitychange();
    assert.equal(signal.aborted, true);
    state.events.pagehide();
    release(response());
    await settle();
    assert.equal(state.timers.size, 0);
    state.document.visibilityState = "visible";
    state.events.pageshow();
    assert.equal(calls, 2);
});
