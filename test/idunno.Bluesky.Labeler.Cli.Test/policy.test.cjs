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
