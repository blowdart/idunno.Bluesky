// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const path = require("node:path");
const { test } = require("node:test");
const vm = require("node:vm");

const script = readFileSync(path.join(__dirname,
    "..", "..", "..", "src", "idunno.Bluesky.AspNet.Labeler.UI", "wwwroot", "js", "team.js"), "utf8");
const actor = { did: "did:plc:test-member", handle: "member.example", displayName: "Example Member" };

class Element {
    classList = {
        values: new Set(),
        add(value) { this.values.add(value); },
        remove(value) { this.values.delete(value); },
        contains(value) { return this.values.has(value); }
    };
    children = [];
    attributes = new Map();
    listeners = new Map();
    selectors = new Map();
    dataset = {};
    value = "";
    textContent = "";
    hidden = false;
    maxLength = 256;
    setAttribute(name, value) { this.attributes.set(name, value); }
    removeAttribute(name) { this.attributes.delete(name); }
    querySelector(selector) { return this.selectors.get(selector); }
    append(...elements) { this.children.push(...elements); }
    replaceChildren() { this.children = []; }
    focus() { this.focused = true; }
    scrollIntoView() { }
    addEventListener(name, callback) {
        const listeners = this.listeners.get(name) ?? [];
        listeners.push(callback);
        this.listeners.set(name, listeners);
    }
    fire(name, fields = {}) {
        const event = { preventDefault() { this.defaultPrevented = true; }, ...fields };
        for (const listener of this.listeners.get(name) ?? []) listener(event);
        return event;
    }
}

function fixture(fetch = async () => ({ ok: true, json: async () => ({ actors: [actor] }) })) {
    const input = new Element();
    input.form = new Element();
    const list = new Element();
    list.id = "enroll-target-suggestions";
    list.hidden = true;
    const status = new Element();
    const container = new Element();
    container.selectors.set("input", input);
    container.selectors.set('[role="listbox"]', list);
    container.selectors.set('[role="status"]', status);
    const form = new Element();
    const action = new Element();
    action.value = "SetRole";
    const role = new Element();
    const roleSelect = new Element();
    role.selectors.set("select", roleSelect);
    const member = new Element();
    member.list = { options: [] };
    form.selectors.set('select[name="action"]', action);
    form.selectors.set("[data-team-role]", role);
    form.selectors.set("[data-team-member]", member);
    const timers = new Map();
    let id = 0;
    const requests = [];
    vm.runInNewContext(script, {
        document: {
            querySelectorAll: selector => selector === "[data-team-change]" ? [form] : [container],
            createElement: () => new Element()
        },
        AbortController,
        URL,
        setTimeout: (callback, delay) => { timers.set(++id, { callback, delay }); return id; },
        clearTimeout: timer => timers.delete(timer),
        fetch: (url, options) => { requests.push({ url, options }); return fetch(url, options); }
    });
    const type = (value, fields) => { input.value = value; input.fire("input", fields); };
    const fireTimers = delay => {
        const pending = [...timers].filter(([, timer]) => timer.delay === delay);
        return pending.map(([key, timer]) => { timers.delete(key); return timer.callback(); });
    };
    return { input, list, status, type, timers, fireTimers, requests, action, role, roleSelect, member };
}

test("debounces direct public searches and never sends cookies or a referrer", async () => {
    const f = fixture();
    f.type("me");
    f.type("@member");
    assert.equal(f.requests.length, 0);
    assert.equal(f.timers.size, 1);
    await Promise.all(f.fireTimers(300));
    assert.equal(f.requests.length, 1);
    const { url, options } = f.requests[0];
    assert.equal(url.origin, "https://public.api.bsky.app");
    assert.equal(url.pathname, "/xrpc/app.bsky.actor.searchActorsTypeahead");
    assert.equal(url.searchParams.get("q"), "member");
    assert.equal(url.searchParams.get("limit"), "8");
    assert.equal(options.credentials, "omit");
    assert.equal(options.referrerPolicy, "no-referrer");
    assert.equal(options.headers, undefined);
    assert.equal(f.list.children.length, 1);
    assert.equal(f.list.children[0].children[0].textContent, "@member.example");
    assert.equal(f.input.attributes.get("aria-expanded"), "true");
});

test("short queries, DIDs and unfinished composition do not request suggestions", () => {
    const f = fixture();
    for (const value of ["", "a", "@a", "did:plc:test-member", "a".repeat(254)]) {
        f.type(value);
        assert.equal(f.timers.size, 0);
    }
    f.type("member", { isComposing: true });
    assert.equal(f.timers.size, 0);
    f.input.fire("compositionend");
    assert.equal(f.timers.size, 1);
    assert.equal(f.requests.length, 0);
});

test("keyboard and pointer selection use the handle without submitting the form", async () => {
    for (const selection of ["keyboard", "pointer"]) {
        const f = fixture();
        f.type("member");
        await Promise.all(f.fireTimers(300));
        if (selection === "keyboard") {
            assert.equal(f.input.fire("keydown", { key: "ArrowUp" }).defaultPrevented, true);
            assert.equal(f.input.attributes.get("aria-activedescendant"), `${f.list.id}-0`);
            assert.equal(f.list.children[0].attributes.get("aria-selected"), "true");
            assert.equal(f.input.fire("keydown", { key: "Enter" }).defaultPrevented, true);
        } else {
            assert.equal(f.list.children[0].fire("pointerdown").defaultPrevented, true);
            f.list.children[0].fire("click");
        }
        assert.equal(f.input.value, actor.handle);
        assert.equal(f.list.hidden, true);
        assert.equal(f.input.attributes.has("aria-activedescendant"), false);
        assert.match(f.status.textContent, /Selected @member.example/);
        assert.equal(f.status.classList.contains("sr-only"), true);
        assert.equal(f.status.hidden, false);
        f.type("other");
        assert.equal(f.input.value, "other");
        assert.equal(f.status.textContent, "");
        assert.equal(f.status.classList.contains("sr-only"), false);
    }
});

test("superseded responses cannot overwrite a newer query or restore a closed list", async () => {
    const completions = [];
    const f = fixture(() => new Promise(resolve => completions.push(resolve)));
    f.type("old");
    const old = f.fireTimers(300)[0];
    f.type("new");
    assert.equal(f.requests[0].options.signal.aborted, true);
    const newer = f.fireTimers(300)[0];
    completions[1]({ ok: true, json: async () => ({ actors: [actor] }) });
    await newer;
    completions[0]({ ok: true, json: async () => ({ actors: [{ ...actor, handle: "old.example" }] }) });
    await old;
    assert.equal(f.list.children[0].children[0].textContent, "@member.example");
    f.type("again");
    const pending = f.fireTimers(300)[0];
    f.input.fire("keydown", { key: "Escape" });
    completions[2]({ ok: true, json: async () => ({ actors: [actor] }) });
    await pending;
    assert.equal(f.list.hidden, true);
    assert.equal(f.status.textContent, "");
});

test("blur and form submission cancel pending searches", () => {
    for (const event of ["blur", "submit"]) {
        const f = fixture();
        f.type("member");
        (event === "blur" ? f.input : f.input.form).fire(event);
        assert.equal(f.timers.size, 0);
        assert.equal(f.list.hidden, true);
        assert.equal(f.requests.length, 0);
    }
});

test("empty, malformed, HTTP and network failures preserve manual entry and show feedback", async () => {
    for (const result of ["empty", "malformed", "http", "network"]) {
        const f = fixture(async () => {
            if (result === "network") throw new Error("Offline");
            return {
                ok: result !== "http",
                json: async () => result === "malformed" ? {} : { actors: [] }
            };
        });
        f.type("member");
        await Promise.all(f.fireTimers(300));
        assert.equal(f.input.value, "member");
        assert.equal(f.list.hidden, true);
        assert.match(f.status.textContent, result === "empty" ? /No matching accounts/ : /unavailable/);
    }
});

test("a request timeout shows an unavailable status", async () => {
    const f = fixture((url, options) => new Promise((resolve, reject) =>
        options.signal.addEventListener("abort", () => reject(new Error("Aborted")))));
    f.type("member");
    const pending = f.fireTimers(300)[0];
    f.fireTimers(8000);
    await pending;
    assert.match(f.status.textContent, /unavailable/);
    assert.equal(f.input.value, "member");
});

test("remote text stays text and invalid entries are discarded", async () => {
    const untrusted = { ...actor, displayName: "<img src=x onerror=alert(1)>" };
    const f = fixture(async () => ({
        ok: true,
        json: async () => ({ actors: [
            null, {}, { ...actor, did: "not-a-did" },
            { ...actor, handle: "handle.invalid" }, { ...actor, handle: "<script>" },
            ...Array(12).fill(untrusted)
        ] })
    }));
    f.type("member");
    await Promise.all(f.fireTimers(300));
    assert.equal(f.list.children.length, 8);
    assert.equal(f.list.children[0].children[1].textContent, untrusted.displayName);
    assert.equal(f.list.children[0].children[1].children.length, 0);
});

test("member selection chooses the registered role while action changes retain existing visibility", () => {
    const f = fixture();
    f.member.list.options = [{ value: "member.example", dataset: { role: "Moderator" } }];
    f.member.value = "@MEMBER.EXAMPLE";
    f.member.fire("input");
    assert.equal(f.roleSelect.value, "Moderator");
    assert.equal(f.role.hidden, false);
    f.action.value = "Suspend";
    f.action.fire("change");
    assert.equal(f.role.hidden, true);
    f.member.value = "unknown.example";
    f.member.fire("change");
    assert.equal(f.roleSelect.value, "Moderator");
    assert.equal(f.requests.length, 0);
});
