// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/audit-filters.js"), "utf8");

function page(saved = "", browserZone = "America/Los_Angeles", fail = false) {
    const local = { disabled: true };
    const zone = { value: saved };
    const name = { textContent: "Local" };
    const tooltip = { textContent: "" };
    const warnings = [];
    runInNewContext(script, {
        document: {
            querySelectorAll(selector) {
                assert.equal(selector, "[data-audit-filters]");
                return [{
                    querySelector(selector) {
                        return { "[data-audit-local]": local, "[data-audit-zone]": zone, "[data-audit-zone-name]": name, "[data-audit-zone-tooltip]": tooltip }[selector];
                    }
                }];
            }
        },
        Intl: {
            DateTimeFormat: function (_, options) {
                if (fail) throw new Error("Time zone unavailable");
                if (options) assert.equal(options.timeZone, saved || browserZone);
                return { resolvedOptions() { return { timeZone: browserZone }; } };
            }
        },
        console: { warn(...args) { warnings.push(args); } }
    });
    return { local, zone, name, tooltip, warnings };
}

test("Local filters use the browser's named zone rather than a fixed offset", () => {
    const current = page();
    assert.equal(current.local.disabled, false);
    assert.equal(current.zone.value, "America/Los_Angeles");
    assert.equal(current.name.textContent, "Local");
    assert.equal(current.name.title, "America/Los_Angeles");
    assert.equal(current.tooltip.textContent, "America/Los_Angeles");
});

test("Paging preserves the zone interpreting existing filter values", () => {
    const current = page("Europe/London");
    assert.equal(current.zone.value, "Europe/London");
    assert.equal(current.name.textContent, "Local");
    assert.equal(current.name.title, "Europe/London");
    assert.equal(current.tooltip.textContent, "Europe/London");
    assert.equal(current.local.disabled, false);
});

for (const [zone, fail] of [["", false], ["America/Los_Angeles", true]]) {
    test(`Unavailable zone (${zone}, ${fail}) leaves local filtering disabled and reports the failure`, () => {
        const current = page("", zone, fail);
        assert.equal(current.local.disabled, true);
        assert.equal(current.name.textContent, "Local");
        assert.equal(current.name.title, "Local time zone unavailable; use UTC.");
        assert.equal(current.warnings.length, 1);
    });
}
