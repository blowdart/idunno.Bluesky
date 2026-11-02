// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/appeal-outcome.js"), "utf8");

function form(baseline, initial = "", canReplace = true, obsolete = false) {
    let manual;
    const labels = baseline.map(applied => ({
        checked: applied,
        closest() { return { dataset: { applied: String(applied) } }; },
        addEventListener(name, callback) { this.change = callback; }
    }));
    const appeal = {
        selectedOptions: [{ dataset: { changed: String(obsolete) } }],
        addEventListener(name, callback) { this.change = callback; }
    };
    const explanation = {};
    const submit = {};
    const outcome = {
        value: initial,
        options: ["", "Upheld", ...(canReplace ? ["Replaced"] : []), "Withdrawn", "Superseded"].map(value => ({ value })),
        closest() { return { querySelector(selector) {
            return {
                'select[name="appealId"]': appeal,
                "[data-appeal-changed]": explanation,
                'button[type="submit"]': submit
            }[selector];
        }, querySelectorAll(selector) {
            assert.equal(selector, 'input[name="selectedLabels"]:not(:disabled)');
            return labels;
        } }; },
        addEventListener(name, callback) { manual = callback; }
    };
    runInNewContext(script, { document: { querySelectorAll() { return [outcome]; } } });
    return {
        labels, outcome, explanation, submit,
        chooseAppeal(changed) { appeal.selectedOptions[0].dataset.changed = String(changed); appeal.change(); },
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

test("Returning to the original selection selects Upheld", () => {
    const page = form([true, false]);
    page.toggle(0);
    assert.equal(page.outcome.value, "Withdrawn");
    page.toggle(0);
    assert.equal(page.outcome.value, "Upheld");
});

test("A single-label policy supports removal and keeping without selecting an unavailable replacement", () => {
    const page = form([true], "", false);
    page.toggle(0);
    assert.equal(page.outcome.value, "Withdrawn");
    page.toggle(0);
    assert.equal(page.outcome.value, "Upheld");
    const addition = form([false], "", false);
    addition.toggle(0);
    assert.equal(addition.outcome.value, "");
});

test("Changed appeal labels are detected on load and cannot be closed with staged label mutations", () => {
    const page = form([true, false], "", true, true);
    assert.equal(page.outcome.value, "Superseded");
    assert.equal(page.explanation.hidden, false);
    assert.equal(page.submit.disabled, false);
    page.toggle(1);
    assert.equal(page.outcome.value, "Superseded");
    assert.equal(page.submit.disabled, true);
    page.toggle(1);
    assert.equal(page.submit.disabled, false);
    page.chooseAppeal(false);
    assert.equal(page.outcome.value, "Upheld");
    assert.equal(page.explanation.hidden, true);
});

test("Manual outcomes remain selectable, but a subsequent label edit updates the outcome", () => {
    const page = form([true, false]);
    page.toggle(1);
    page.select("Upheld");
    assert.equal(page.outcome.value, "Upheld");
    page.toggle(0);
    assert.equal(page.outcome.value, "Replaced");
});
