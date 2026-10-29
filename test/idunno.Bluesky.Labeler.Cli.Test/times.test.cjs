// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/times.js"), "utf8");

function page(saved = null, failStorage = false, dates = true, relative = false, offset = 0) {
    let clicked;
    let tick;
    let now = Date.parse("2026-01-01T00:00:00Z") + offset;
    class Clock extends Date {
        static now() { return now; }
    }
    const attributes = new Map([["aria-pressed", "false"]]);
    const button = {
        hidden: true,
        getAttribute(name) { return attributes.get(name); },
        setAttribute(name, value) { attributes.set(name, value); },
        addEventListener(name, callback) { assert.equal(name, "click"); clicked = callback; }
    };
    const zone = { textContent: "UTC" };
    const controls = { hidden: true };
    const status = { textContent: "" };
    const stored = new Map(saved === null ? [] : [["xenon.timeZone", saved]]);
    const warnings = [];
    const time = {
        textContent: "2026-01-01 00:00:00Z", dateTime: "2026-01-01T00:00:00.0000000+00:00",
        hasAttribute() { return relative; },
        classList: { add(value) { assert.equal(value, "identity"); } },
        setAttribute(name, value) { this[name] = value; },
        replaceChildren(...children) { this.children = children; }
    };
    const invalid = { textContent: "Unavailable", dateTime: "invalid", hasAttribute() { return false; } };
    runInNewContext(script, {
        document: {
            createElement() {
                return { setAttribute(name, value) { this[name] = value; } };
            },
            querySelectorAll() {
                return [{
                    querySelector(selector) {
                        return selector === "[data-time-zone-toggle]" ? button :
                            selector === "[data-time-controls]" ? controls :
                            selector === "[data-time-status]" ? status : zone;
                    },
                    querySelectorAll(selector) { assert.equal(selector, "time[datetime]"); return dates ? [time, invalid] : [invalid]; }
                }];
            }
        },
        localStorage: {
            getItem(key) {
                if (failStorage) throw new Error("Storage blocked");
                return stored.get(key) ?? null;
            },
            setItem(key, value) {
                if (failStorage) throw new Error("Storage blocked");
                stored.set(key, value);
            }
        },
        console: { warn(...args) { warnings.push(args); } },
        Intl, Date: Clock,
        setInterval(callback, milliseconds) { assert.equal(milliseconds, 60000); tick = callback; }
    });
    return {
        button, controls, status, stored, warnings, attributes, zone, time, invalid,
        click() { clicked(); },
        advance(milliseconds) { now += milliseconds; tick(); }
    };
}

test("Page clock toggles all dates and accessible state without altering canonical timestamps", () => {
    const { button, controls, attributes, time, zone, invalid, stored, click } = page();
    const formatter = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "long" });
    assert.equal(button.hidden, false);
    assert.equal(controls.hidden, false);
    click();
    assert.equal(stored.get("xenon.timeZone"), "local");
    assert.equal(attributes.get("aria-pressed"), "true");
    assert.equal(attributes.get("aria-label"), "Switch displayed dates to UTC");
    assert.equal(button.title, "Switch displayed dates to UTC.");
    assert.equal(time.textContent, formatter.format(new Date(time.dateTime)));
    assert.equal(zone.textContent, "Local");
    assert.equal(invalid.textContent, "Unavailable");
    click();
    assert.equal(stored.get("xenon.timeZone"), "utc");
    assert.equal(time.textContent, "2026-01-01 00:00:00Z");
    assert.equal(time.dateTime, "2026-01-01T00:00:00.0000000+00:00");
    assert.equal(zone.textContent, "UTC");
    assert.equal(attributes.get("aria-pressed"), "false");
    assert.equal(attributes.get("aria-label"), "Switch displayed dates to your local time zone");
    assert.equal(button.title, "Switch displayed dates to your local time zone.");
});

for (const [seconds, text] of [
    [0, "Just now"], [59, "Just now"], [60, "1 minute ago"],
    [300, "5 minutes ago"], [3600, "1 hour ago"], [86400, "1 day ago"],
    [604800, "1 week ago"], [2592000, "1 month ago"], [31536000, "1 year ago"],
    [-120, "in 2 minutes"]
]) {
    test(`Relative list date at ${seconds} seconds preserves the exact timestamp`, () => {
        const current = page(null, false, true, true, seconds * 1000);
        const [label, tooltip] = current.time.children;
        assert.equal(label.textContent, text);
        assert.equal(tooltip.textContent, "2026-01-01 00:00:00Z");
        assert.equal(tooltip.className, "identity-tooltip");
        assert.equal(tooltip.role, "tooltip");
        assert.equal(current.time["aria-describedby"], tooltip.id);
        assert.equal(current.time.tabIndex, 0);
        current.click();
        assert.equal(label.textContent, text);
        assert.equal(tooltip.textContent, new Intl.DateTimeFormat(undefined, {
            dateStyle: "medium", timeStyle: "long"
        }).format(new Date(current.time.dateTime)));
        assert.equal(current.time.dateTime, "2026-01-01T00:00:00.0000000+00:00");
    });
}

test("Relative dates refresh with the clock and work when preference storage is blocked", () => {
    const current = page(null, true, true, true, 60000);
    assert.equal(current.time.children[0].textContent, "1 minute ago");
    assert.equal(current.time.children[1].textContent, "2026-01-01 00:00:00Z");
    current.advance(60000);
    assert.equal(current.time.children[0].textContent, "2 minutes ago");
});

test("Time zone preference remains available on pages without valid dates", () => {
    const current = page("local", false, false);
    assert.equal(current.button.hidden, false);
    assert.equal(current.controls.hidden, false);
    assert.equal(current.zone.textContent, "Local");
    current.click();
    assert.equal(current.zone.textContent, "UTC");
    assert.equal(current.stored.get("xenon.timeZone"), "utc");
    const next = page(current.stored.get("xenon.timeZone"));
    assert.equal(next.zone.textContent, "UTC");
    assert.equal(next.time.textContent, "2026-01-01 00:00:00Z");
});

test("Pages without dates report blocked storage and still allow switching", () => {
    const current = page(null, true, false);
    assert.equal(current.controls.hidden, false);
    assert.match(current.status.textContent, /Changes apply to this page only/);
    current.click();
    assert.equal(current.zone.textContent, "Local");
    assert.equal(current.warnings.length, 2);
});

test("Saved local choice is restored on another page and UTC choice is retained", () => {
    const first = page();
    first.click();
    const next = page(first.stored.get("xenon.timeZone"));
    assert.equal(next.attributes.get("aria-pressed"), "true");
    const formatter = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "long" });
    assert.equal(next.time.textContent, formatter.format(new Date(next.time.dateTime)));
    next.click();
    const utc = page(next.stored.get("xenon.timeZone"));
    assert.equal(utc.attributes.get("aria-pressed"), "false");
    assert.equal(utc.time.textContent, "2026-01-01 00:00:00Z");
});

for (const saved of [null, "", "invalid", "utc"]) {
    test(`Missing or invalid choice ${JSON.stringify(saved)} starts in UTC`, () => {
        const current = page(saved);
        assert.equal(current.attributes.get("aria-pressed"), "false");
        assert.equal(current.zone.textContent, "UTC");
    });
}

test("Blocked storage reports the failure and leaves page-only toggling available", () => {
    const current = page(null, true);
    assert.match(current.status.textContent, /Changes apply to this page only/);
    assert.equal(current.warnings.length, 1);
    current.click();
    assert.equal(current.attributes.get("aria-pressed"), "true");
    assert.match(current.status.textContent, /Changes apply to this page only/);
    assert.equal(current.warnings.length, 2);
    current.click();
    assert.equal(current.time.textContent, "2026-01-01 00:00:00Z");
});
