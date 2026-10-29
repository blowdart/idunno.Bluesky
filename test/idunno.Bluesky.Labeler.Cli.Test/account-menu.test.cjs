// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const script = fs.readFileSync(path.join(__dirname, "..", "..", "src",
    "idunno.Bluesky.AspNet.Labeler.UI", "wwwroot", "js", "identities.js"), "utf8");

function harness() {
    const listeners = {};
    const inside = {};
    let open = true;
    let focused = false;
    const menu = {
        contains: target => target === inside,
        removeAttribute: name => { assert.equal(name, "open"); open = false; },
        querySelector: selector => {
            assert.equal(selector, "summary");
            return { focus: () => { focused = true; } };
        }
    };
    const document = {
        addEventListener: (name, callback) => { listeners[name] = callback; },
        querySelectorAll: selector => selector === ".account-menu[open]" && open ? [menu] : []
    };
    vm.runInNewContext(script, { document });
    return { listeners, inside, isOpen: () => open, isFocused: () => focused };
}

test("Escape closes the account dropdown and returns focus to its handle", () => {
    const view = harness();
    view.listeners.keydown({ key: "Escape" });
    assert.equal(view.isOpen(), false);
    assert.equal(view.isFocused(), true);
});

test("Other keys do not close the native dropdown", () => {
    const view = harness();
    view.listeners.keydown({ key: "Tab" });
    assert.equal(view.isOpen(), true);
    assert.equal(view.isFocused(), false);
});

test("Clicks inside preserve the dropdown; outside clicks close without stealing focus", () => {
    const view = harness();
    view.listeners.click({ target: view.inside });
    assert.equal(view.isOpen(), true);
    view.listeners.click({ target: {} });
    assert.equal(view.isOpen(), false);
    assert.equal(view.isFocused(), false);
});
