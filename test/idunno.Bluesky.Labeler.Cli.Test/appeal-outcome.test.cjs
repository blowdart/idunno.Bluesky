// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/appeal-outcome.js"), "utf8");

function form(baseline, initial = "") {
    let manual;
    const labels = baseline.map(applied => ({
        checked: applied,
        closest() { return { dataset: { applied: String(applied) } }; },
        addEventListener(name, callback) { this.change = callback; }
    }));
    const outcome = {
        value: initial,
        closest() { return { querySelectorAll(selector) {
            assert.equal(selector, 'input[name="selectedLabels"]:not(:disabled)');
            return labels;
        } }; },
        addEventListener(name, callback) { manual = callback; }
    };
    runInNewContext(script, { document: { querySelectorAll() { return [outcome]; } } });
    return {
        labels, outcome,
        toggle(index) { labels[index].checked = !labels[index].checked; labels[index].change(); },
        select(value) { outcome.value = value; manual(); }
    };
}

test("Removing all applied labels selects Withdrawn; partial removal selects Replaced", () => {
    const page = form([true, true, false]);
    page.toggle(0);
    assert.equal(page.outcome.value, "Replaced");
    page.toggle(1);
    assert.equal(page.outcome.value, "Withdrawn");
    page.toggle(2);
    assert.equal(page.outcome.value, "Replaced");
});

test("Adding one or all labels selects Replaced without changing initial state on load", () => {
    const page = form([false, false]);
    assert.equal(page.outcome.value, "");
    page.toggle(0);
    assert.equal(page.outcome.value, "Replaced");
    page.toggle(1);
    assert.equal(page.outcome.value, "Replaced");
});

test("Returning to the original selection restores the previous explicit outcome", () => {
    const page = form([true, false], "Superseded");
    page.toggle(0);
    assert.equal(page.outcome.value, "Withdrawn");
    page.toggle(0);
    assert.equal(page.outcome.value, "Superseded");
});

test("Manual outcomes remain selectable, but a subsequent label edit updates the outcome", () => {
    const page = form([true, false]);
    page.toggle(1);
    page.select("Upheld");
    assert.equal(page.outcome.value, "Upheld");
    page.toggle(0);
    assert.equal(page.outcome.value, "Replaced");
});
