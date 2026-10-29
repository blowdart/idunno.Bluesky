// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/media-warning.js"), "utf8");

function preview() {
    const state = { blurred: true };
    const buttons = Array.from({ length: 2 }, () => {
        let click;
        const attributes = { "aria-label": "Show images", "aria-pressed": "false" };
        const classes = new Set(["fa-eye"]);
        return {
            attributes, classes, hidden: true,
            addEventListener(name, callback) { assert.equal(name, "click"); click = callback; },
            setAttribute(name, value) { attributes[name] = value; },
            querySelector(selector) {
                assert.equal(selector, "[data-media-icon]");
                return { classList: { toggle(name, enabled) { if (enabled) classes.add(name); else classes.delete(name); } } };
            },
            click() { click(); }
        };
    });
    const images = Array.from({ length: 2 }, () => ({
        hidden: true,
        setAttribute(name, value) { assert.equal(name, "aria-hidden"); assert.equal(value, "true"); this.hidden = true; },
        removeAttribute(name) { assert.equal(name, "aria-hidden"); this.hidden = false; }
    }));
    const element = {
        classList: {
            toggle(name) {
                assert.equal(name, "media-blurred");
                state.blurred = !state.blurred;
                return state.blurred;
            }
        },
        querySelectorAll(selector) {
            if (selector === "[data-media-toggle]") return buttons;
            assert.equal(selector, "[data-sensitive-image]");
            return images;
        }
    };
    return { element, buttons, images, state };
}

function load(previews) {
    runInNewContext(script, {
        document: {
            querySelectorAll(selector) {
                assert.equal(selector, '[data-media-warning="true"]');
                return previews.map(preview => preview.element);
            }
        }
    });
}

test("Only overlay buttons reveal images and synchronize all overlay icons and accessible labels", () => {
    const item = preview();
    load([item]);
    assert.equal(item.state.blurred, true);
    assert.ok(item.buttons.every(button => !button.hidden));
    assert.ok(item.images.every(image => image.hidden));
    item.buttons[0].click();
    assert.equal(item.state.blurred, false);
    assert.ok(item.images.every(image => !image.hidden));
    for (const button of item.buttons) {
        assert.equal(button.attributes["aria-label"], "Blur images");
        assert.equal(button.attributes.title, "Blur images");
        assert.equal(button.attributes["aria-pressed"], "true");
        assert.ok(button.classes.has("fa-eye-slash"));
        assert.ok(!button.classes.has("fa-eye"));
    }
    item.buttons[1].click();
    assert.equal(item.state.blurred, true);
    assert.ok(item.images.every(image => image.hidden));
    for (const button of item.buttons) {
        assert.equal(button.attributes["aria-label"], "Show images");
        assert.equal(button.attributes["aria-pressed"], "false");
        assert.ok(button.classes.has("fa-eye"));
        assert.ok(!button.classes.has("fa-eye-slash"));
    }
});

test("Revealing one preview does not reveal others or persist across page loads", () => {
    const first = preview();
    const second = preview();
    load([first, second]);
    first.buttons[0].click();
    assert.equal(first.state.blurred, false);
    assert.equal(second.state.blurred, true);
    const reloaded = preview();
    load([reloaded]);
    assert.equal(reloaded.state.blurred, true);
});

test("Pages without warnings need no controls", () => load([]));
