// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/policy.js"), "utf8");
const context = { document: { querySelectorAll() { return []; } } };
runInNewContext(script, context);
const parse = text => context.parsePolicyDeclaration(text);
const initial = () => ({
    $type: "app.bsky.labeler.service",
    createdAt: "2026-01-01T00:00:00Z",
    subjectTypes: ["account", "record"],
    policies: {
        labelValues: ["spam", "custom"],
        labelValueDefinitions: [{
            identifier: "custom", severity: "inform", blurs: "none", defaultSetting: "warn",
            adultOnly: false, locales: [{ lang: "en", name: "Custom", description: "Explanation", extension: 1 }],
            extension: { retained: true }
        }],
        extension: "policy extension"
    },
    extension: { retained: true }
});

test("Policy tabs retain their fragments on refresh and draft Publish shortcuts select without submitting", () => {
    const callbacks = {};
    const tabCallbacks = [];
    const panels = ["policy-current", "policy-labels", "policy-moderation", "policy-advanced", "policy-publication", "policy-verification"]
        .map(id => ({ id, setAttribute() {} }));
    const tabs = panels.map((panel, index) => ({
        getAttribute() { return "#" + panel.id; },
        setAttribute() {},
        focus() {},
        addEventListener(event, callback) { (tabCallbacks[index] ??= {})[event] = callback; }
    }));
    const location = { hash: "#policy-publication" };
    const history = {
        state: { retained: true },
        replaceState(state, unused, hash) {
            assert.equal(state, this.state);
            location.hash = hash;
        }
    };
    const selection = { value: "", focus() { this.focused = true; } };
    const link = {
        dataset: { policyPublish: "v3" },
        addEventListener(event, callback) { callbacks[event] = callback; }
    };
    const root = {
        dataset: {},
        addEventListener() {},
        querySelectorAll(selector) {
            if (selector === "[data-policy-tab]") return tabs;
            if (selector === "[data-policy-panel]") return panels;
            if (selector === "[data-policy-publish]") return [link];
            return [];
        },
        querySelector(selector) {
            if (selector === "nav") return { setAttribute() {} };
            if (selector.startsWith("#")) return panels.find(panel => "#" + panel.id === selector);
            if (selector === 'form[action*="handler=Publish"] select[name="version"]') return selection;
            return null;
        }
    };
    const pageContext = {
        document: { querySelectorAll() { return [root]; } },
        location,
        history
    };
    runInNewContext(script, pageContext);
    assert.equal(panels[4].hidden, false);
    for (let index = 0; index < tabs.length; index++) {
        tabCallbacks[index].click({ preventDefault() {} });
        assert.equal(location.hash, "#" + panels[index].id);
        runInNewContext(script, pageContext);
        assert.equal(panels[index].hidden, false);
        assert.equal(panels.filter(panel => !panel.hidden).length, 1);
    }
    tabCallbacks[0].click({ preventDefault() {} });
    assert.equal(panels[0].hidden, false);
    assert.equal(location.hash, "#policy-current");
    tabCallbacks[0].keydown({ key: "ArrowRight", preventDefault() {} });
    assert.equal(location.hash, "#policy-labels");
    assert.equal(panels[1].hidden, false);
    tabCallbacks[1].keydown({ key: "Home", preventDefault() {} });
    assert.equal(location.hash, "#policy-current");
    let prevented = false;
    callbacks.click({ preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
    assert.equal(selection.value, "v3");
    assert.equal(selection.focused, true);
    assert.equal(panels[4].hidden, false);
    assert.equal(panels[0].hidden, true);
    assert.equal(location.hash, "#policy-publication");
});

test("Valid declarations retain label metadata, translations and unknown extensions", () => {
    const value = initial();
    assert.equal(JSON.stringify(parse(JSON.stringify(value))), JSON.stringify(value));
});

for (const [title, alter] of [
    ["missing policies", value => { delete value.policies; }],
    ["null policies", value => { value.policies = null; }],
    ["invalid labels", value => { value.policies.labelValues = [null]; }],
    ["duplicate labels", value => { value.policies.labelValues.push("spam"); }],
    ["unknown subject type", value => { value.subjectTypes = ["unknown"]; }],
    ["null reason restriction", value => { value.reasonTypes = null; }],
    ["invalid collections", value => { value.subjectCollections = [2]; }],
    ["invalid definition", value => { value.policies.labelValueDefinitions = [null]; }],
    ["undeclared definition", value => { value.policies.labelValueDefinitions[0].identifier = "orphan"; }],
    ["duplicate definition", value => { value.policies.labelValueDefinitions.push(value.policies.labelValueDefinitions[0]); }],
    ["invalid translation", value => { value.policies.labelValueDefinitions[0].locales = [{}]; }]
]) {
    test(`Rejects ${title} rather than resetting structured controls`, () => {
        const value = initial();
        alter(value);
        assert.throws(() => parse(JSON.stringify(value)));
    });
}

test("Malformed JSON remains an explicit error", () => {
    assert.throws(() => parse('{"policies":'));
});

test("Report controls distinguish all reasons from none and preserve unrelated declaration fields", () => {
    const value = initial();
    context.setPolicyReportScope(value, ["record"], ["app.bsky.feed.post"], []);
    assert.deepEqual(value.reasonTypes, []);
    assert.deepEqual(value.subjectTypes, ["record"]);
    assert.deepEqual(value.subjectCollections, ["app.bsky.feed.post"]);
    assert.deepEqual(value.policies, initial().policies);
    assert.deepEqual(value.extension, { retained: true });
    context.setPolicyReportScope(value, [], null, null);
    assert.equal("reasonTypes" in value, false);
    assert.equal("subjectCollections" in value, false);
    assert.deepEqual(value.subjectTypes, []);
});
