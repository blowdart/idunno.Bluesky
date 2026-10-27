// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");

const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/theme.js"), "utf8");
const css = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/css/xenon.css"), "utf8");

function browser(saved, failStorage = false) {
    const attributes = new Map();
    const events = {};
    const control = {
        disabled: true,
        value: "",
        addEventListener(name, callback) { events[name] = callback; }
    };
    const status = { textContent: "" };
    const storage = {
        getItem(key) {
            assert.equal(key, "xenon.theme");
            if (failStorage) throw new Error("Storage blocked");
            return saved;
        },
        setItem(key, value) {
            assert.equal(key, "xenon.theme");
            if (failStorage) throw new Error("Storage blocked");
            saved = value;
        },
        removeItem(key) {
            assert.equal(key, "xenon.theme");
            if (failStorage) throw new Error("Storage blocked");
            saved = null;
        }
    };
    runInNewContext(script, {
        document: {
            documentElement: {
                setAttribute(name, value) { attributes.set(name, value); },
                removeAttribute(name) { attributes.delete(name); }
            },
            getElementById(id) { return id === "theme-choice" ? control : status; },
            addEventListener(name, callback) { events[name] = callback; }
        },
        window: { addEventListener(name, callback) { events[name] = callback; } },
        localStorage: storage,
        console: { warn() {} }
    });
    return {
        attributes, control, status, events, storage,
        saved: () => saved,
        ready: () => events.DOMContentLoaded(),
        choose(value) { control.value = value; events.change(); }
    };
}

for (const value of ["light", "dark"]) {
    test(`Restores ${value} before document rendering and remembers a changed choice`, () => {
        const page = browser(value);
        assert.equal(page.attributes.get("data-theme"), value);
        page.ready();
        assert.equal(page.control.value, value);
        assert.equal(page.control.disabled, false);
        const next = value === "dark" ? "light" : "dark";
        page.choose(next);
        assert.equal(page.attributes.get("data-theme"), next);
        assert.equal(page.saved(), next);
        assert.equal(browser(page.saved()).attributes.get("data-theme"), next);
    });
}

for (const value of [null, "", "invalid", "system"]) {
    test(`Missing or invalid choice ${JSON.stringify(value)} follows the OS without an override`, () => {
        const page = browser(value);
        assert.equal(page.attributes.has("data-theme"), false);
        page.ready();
        assert.equal(page.control.value, "system");
    });
}

test("Selecting System removes the saved override and restores live OS-driven CSS", () => {
    const page = browser("dark");
    page.ready();
    page.choose("system");
    assert.equal(page.saved(), null);
    assert.equal(page.attributes.has("data-theme"), false);
    assert.match(css, /@media \(prefers-color-scheme: dark\)\s*\{\s*:root:not\(\[data-theme="light"\]\)/);
    assert.match(css, /:root\[data-theme="dark"\]/);
});

test("Other tabs update the selection and clearing storage restores System", () => {
    const page = browser("light");
    page.ready();
    page.events.storage({ storageArea: page.storage, key: "xenon.theme", newValue: "dark" });
    assert.equal(page.control.value, "dark");
    assert.equal(page.attributes.get("data-theme"), "dark");
    page.events.storage({ storageArea: page.storage, key: null, newValue: null });
    assert.equal(page.control.value, "system");
    assert.equal(page.attributes.has("data-theme"), false);
    page.events.storage({ storageArea: page.storage, key: "unrelated", newValue: "dark" });
    assert.equal(page.control.value, "system");
});

test("Storage failures keep the selector working and explicitly explain that preference cannot persist", () => {
    const page = browser(null, true);
    page.ready();
    assert.match(page.status.textContent, /this page only/);
    page.choose("dark");
    assert.equal(page.attributes.get("data-theme"), "dark");
    assert.match(page.status.textContent, /could not save or read/);
    page.choose("system");
    assert.equal(page.attributes.has("data-theme"), false);
});
