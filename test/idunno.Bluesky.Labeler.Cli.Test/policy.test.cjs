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
    let deletionConfirmation = null;
    const editorTab = {};
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
            if (selector === 'input[name="editorTab"]') return editorTab;
            if (selector === "[data-policy-delete-confirmation]") return deletionConfirmation;
            if (selector === "nav") return { setAttribute() {} };
            if (selector.startsWith("#")) return panels.find(panel => "#" + panel.id === selector);
            if (selector === 'form[action*="handler=Publish"] select[name="version"]') return selection;
            return null;
        }
    };
    const pageContext = {
        document: { querySelectorAll() { return [root]; } },
        window: { addEventListener(event, callback, options) {
            assert.equal(event, "load");
            assert.equal(options.once, true);
            callbacks.load = callback;
        } },
        location,
        history
    };
    runInNewContext(script, pageContext);
    assert.equal(panels[4].hidden, false);
    for (const fragment of ["", "#main"]) {
        location.hash = fragment;
        runInNewContext(script, pageContext);
        assert.equal(location.hash, fragment);
        assert.equal(panels[0].hidden, false);
    }
    for (const retained of ["policy-labels", "policy-moderation", "policy-advanced"]) {
        location.hash = "#main";
        editorTab.value = retained;
        runInNewContext(script, pageContext);
        assert.equal(location.hash, "#main");
        assert.equal(panels.find(panel => panel.id === retained).hidden, false);
    }
    for (let index = 0; index < tabs.length; index++) {
        tabCallbacks[index].click({ preventDefault() {} });
        assert.equal(location.hash, "#" + panels[index].id);
        if ([1, 2, 3].includes(index)) assert.equal(editorTab.value, panels[index].id);
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
    const reason = { focus(options) { this.focused = options; } };
    deletionConfirmation = {
        querySelector(selector) {
            assert.equal(selector, 'input[name="reason"]');
            assert.equal(panels[0].hidden, false);
            return reason;
        },
        scrollIntoView(options) { this.scrolled = options; }
    };
    for (const fragment of ["#policy-current", "#policy-labels", "#policy-publication"]) {
        location.hash = fragment;
        runInNewContext(script, pageContext);
        assert.equal(location.hash, fragment);
        assert.equal(panels[0].hidden, false);
        assert.equal(typeof callbacks.load, "function");
        callbacks.load();
        assert.equal(reason.focused.preventScroll, true);
        assert.equal(deletionConfirmation.scrolled.block, "start");
    }
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

test("Account reports require profile compatibility only for restricted collections without removing explicit profile support", () => {
    for (const subjects of [["account"], ["account", "record"]]) {
        for (const collections of [[], ["app.bsky.feed.post"], ["app.bsky.actor.profile"]]) {
            const value = initial();
            context.setPolicyReportScope(value, subjects, collections, null);
            assert.equal(value.subjectCollections.filter(item => item === "app.bsky.actor.profile").length, 1);
            assert.deepEqual(Array.from(value.subjectCollections), [...new Set([...collections, "app.bsky.actor.profile"])]);
            context.setPolicyReportScope(value, ["record"], value.subjectCollections, null);
            assert.ok(value.subjectCollections.includes("app.bsky.actor.profile"));
        }
        const value = initial();
        context.setPolicyReportScope(value, subjects, null, null);
        assert.equal("subjectCollections" in value, false);
    }
});

test("Loading stored policy JSON reflects exact scope without applying account compatibility changes", () => {
    for (const collections of [undefined, [], ["app.bsky.feed.post"], ["app.bsky.feed.post", "app.bsky.actor.profile"]]) {
        const value = initial();
        if (collections !== undefined) value.subjectCollections = collections;
        const original = JSON.stringify(value);
        const fields = {
            reasonMode: {}, customReasons: {}, reportCollections: {}, reportAllCollections: {}
        };
        const subjects = [{ value: "account" }, { value: "record" }];
        const page = {
            raw: { value: original, setCustomValidity(message) { this.validationMessage = message; } },
            error: {}, add: {}, syncIssuance: {},
            form: { querySelectorAll(selector) { return selector === 'input[name="reportSubjects"]' ? subjects : []; } },
            field: name => fields[name],
            parsePolicyDeclaration: context.parsePolicyDeclaration,
            renderLabels() {},
            reportControls() { assert.fail("Loading must not rewrite report scope"); }
        };
        const start = script.indexOf("    const reflect = ");
        const end = script.indexOf('    raw.addEventListener("input", read);', start);
        runInNewContext(script.slice(start, end) + "\nthis.read = read;", page);
        page.read();
        assert.equal(page.raw.value, original);
        assert.equal(fields.reportCollections.value, (collections ?? []).join(", "));
        assert.equal(fields.reportAllCollections.checked, collections === undefined);
        assert.equal(subjects.every(subject => subject.checked), true);
    }
});

test("Backspacing through required profile collection does not rewrite the field or accumulate partial identifiers", () => {
    const declaration = initial();
    let submittedDeclaration;
    const fields = {
        reasonMode: { value: "all" },
        reportAllCollections: { checked: false },
        reportCollections: { value: "app.bsky.feed.post, app.bsky.actor.profile" }
    };
    const editing = {
        declaration,
        field: name => fields[name],
        form: {
            querySelectorAll(selector) {
                return selector.includes("reportSubjects") ? [{ value: "account" }, { value: "record" }] : [];
            }
        },
        list: (value, separator) => value.split(separator).map(item => item.trim()).filter(Boolean),
        write() { submittedDeclaration = JSON.stringify(declaration); },
        setPolicyReportScope: context.setPolicyReportScope
    };
    const start = script.indexOf("    const reportControls = ");
    const end = script.indexOf("    const input = ", start);
    runInNewContext(script.slice(start, end) + "\nthis.update = reportControls;", editing);
    while (fields.reportCollections.value !== "app.bsky.feed.post") {
        const typed = fields.reportCollections.value.slice(0, -1);
        fields.reportCollections.value = typed;
        editing.update();
        assert.equal(fields.reportCollections.value, typed);
        assert.equal(declaration.subjectCollections.filter(item => item === "app.bsky.actor.profile").length, 1);
        assert.deepEqual(JSON.parse(submittedDeclaration).subjectTypes, ["account", "record"]);
    }
    editing.update(true);
    assert.equal(fields.reportCollections.value, "app.bsky.feed.post, app.bsky.actor.profile");
    editing.update(true);
    assert.equal(fields.reportCollections.value, "app.bsky.feed.post, app.bsky.actor.profile");
    fields.reportCollections.value = "";
    editing.update();
    editing.update(true);
    assert.deepEqual(JSON.parse(submittedDeclaration).subjectTypes, ["account", "record"]);
    assert.deepEqual(JSON.parse(submittedDeclaration).subjectCollections, ["app.bsky.actor.profile"]);
    fields.reportCollections.value = "app.bsky.feed.post, app.bsky.actor.profile";
    editing.update();
    editing.update(true);
    assert.deepEqual(JSON.parse(submittedDeclaration).subjectTypes, ["account", "record"]);
    assert.deepEqual(JSON.parse(submittedDeclaration).subjectCollections, ["app.bsky.feed.post", "app.bsky.actor.profile"]);
});

test("Matching label permissions copies current declaration scope without editing or publishing the declaration", () => {
    const start = script.indexOf("    syncIssuance.hidden = false;");
    const end = script.indexOf('    ["reasonMode"', start);
    for (const subjects of [[], ["account"], ["record"], ["account", "record"]]) {
        for (const collections of [undefined, [], ["app.bsky.feed.post", "app.bsky.actor.profile"]]) {
            const declaration = initial();
            declaration.subjectTypes = subjects;
            if (collections !== undefined) declaration.subjectCollections = collections;
            const original = JSON.stringify(declaration);
            const fields = {
                allowAccounts: { checked: true }, allowRecords: { checked: true },
                issueAllCollections: { checked: false }, collections: { value: "old.collection" }
            };
            let click;
            const page = {
                declaration,
                raw: { validationMessage: "" },
                syncIssuance: { addEventListener(event, callback) { assert.equal(event, "click"); click = callback; } },
                field: name => fields[name]
            };
            runInNewContext(script.slice(start, end), page);
            assert.equal(page.syncIssuance.hidden, false);
            click();
            assert.equal(fields.allowAccounts.checked, subjects.includes("account"));
            assert.equal(fields.allowRecords.checked, subjects.includes("record"));
            assert.equal(fields.issueAllCollections.checked, collections === undefined);
            assert.equal(fields.collections.value, (collections ?? []).join(", "));
            assert.equal(JSON.stringify(declaration), original);
            fields.collections.value = "manual.override";
            page.raw.validationMessage = "Invalid JSON";
            click();
            assert.equal(fields.collections.value, "manual.override");
        }
    }
});

test("Loading a policy preserves the active editor tab and defaults to Labels outside the editor", () => {
    for (const active of ["#policy-labels", "#policy-moderation", "#policy-advanced", "#policy-current", "#policy-publication"]) {
        const fields = {};
        const heading = {};
        const publish = { dataset: {} };
        const tabs = ["#policy-current", "#policy-labels", "#policy-moderation", "#policy-advanced", "#policy-publication"]
            .map(href => ({ getAttribute(name) { return name === "href" ? href : String(href === active); } }));
        let selected;
        const page = {
            raw: {},
            field(name) { return fields[name] ??= {}; },
            root: { querySelector(selector) {
                if (selector === "[data-policy-editor-publish]") return publish;
                return selector === "[data-policy-save-heading]" ? heading : { dataset: {}, querySelector() { return {}; } };
            } },
            tabs,
            read() {},
            show(tab) { selected = tab.getAttribute("href"); }
        };
        const start = script.indexOf("    const load = ");
        const end = script.indexOf('    root.querySelectorAll("[data-policy-load]")', start);
        runInNewContext(script.slice(start, end) + "\nthis.load = load;", page);
        page.load({ declaration: initial(), allowAccounts: true, allowRecords: true, rules: "Retained" }, "Current policy");
        assert.equal(selected, ["#policy-labels", "#policy-moderation", "#policy-advanced"].includes(active) ? active : "#policy-labels");
        page.load({ version: "draft-v4", declaration: initial(), allowAccounts: true, allowRecords: true, rules: "Retained" }, "Saved draft", true);
        assert.equal(fields.version.value, "draft-v4");
        assert.equal(fields.version.readOnly, true);
        assert.equal(fields.editingDraft.value, "draft-v4");
        assert.equal(heading.textContent, "Update a draft policy");
        assert.equal(publish.hidden, false);
        assert.equal(publish.dataset.policyPublish, "draft-v4");
        assert.equal(publish.href, "?publish=draft-v4#policy-publication");
        page.load({ declaration: initial(), allowAccounts: true, allowRecords: true, rules: "Retained" }, "Current policy");
        assert.equal(fields.version.value, "");
        assert.equal(fields.version.readOnly, false);
        assert.equal(fields.editingDraft.value, "");
        assert.equal(heading.textContent, "Save a policy draft");
        assert.equal(publish.hidden, true);
        assert.equal(publish.dataset.policyPublish, "");
    }
});
