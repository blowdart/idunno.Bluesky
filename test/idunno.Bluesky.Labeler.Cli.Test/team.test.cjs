// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/team.js"), "utf8");

for (const initial of ["SetRole", "Suspend", "Reinstate", "Remove"]) {
    test(`Role visibility follows action changes from ${initial}`, () => {
        const role = { hidden: false };
        const action = {
            value: initial,
            addEventListener(name, callback) {
                assert.equal(name, "change");
                this.change = callback;
            }
        };
        const form = {
            querySelector(selector) {
                return selector === '[data-team-role]' ? role : action;
            }
        };
        runInNewContext(script, { document: { querySelectorAll() { return [form]; } } });
        assert.equal(role.hidden, initial !== "SetRole");
        for (const value of ["Suspend", "Reinstate", "Remove", "SetRole"]) {
            action.value = value;
            action.change();
            assert.equal(role.hidden, value !== "SetRole");
        }
    });
}

test("Pages without a membership form are unchanged", () => {
    runInNewContext(script, { document: { querySelectorAll() { return []; } } });
});
