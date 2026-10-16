// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

// Exercise the actual browser module without introducing a package or DOM-emulation dependency.
const source = await readFile(new URL("../../samples/Samples.BlazorAuthentication/wwwroot/profileDraft.js", import.meta.url), "utf8");
const { attach } = await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);

function browser(t) {
    // Emulate only the inputs and per-tab storage used by the module. Keeping storage across
    // editor instances models expiry and sign-in while making account isolation observable.
    const values = new Map();
    const inputs = new Map(["display-name", "description", "pronouns"].map(id => [id, { value: "" }]));
    let changed;
    const form = {
        addEventListener: (_, callback) => changed = callback,
        removeEventListener: () => changed = undefined
    };
    const previousDocument = globalThis.document;
    const previousStorage = globalThis.sessionStorage;
    globalThis.document = { getElementById: id => id === "profile-editor" ? form : inputs.get(id) };
    globalThis.sessionStorage = {
        getItem: key => values.get(key) ?? null,
        setItem: (key, value) => values.set(key, value),
        removeItem: key => values.delete(key)
    };
    t.after(() => {
        if (previousDocument === undefined) delete globalThis.document;
        else globalThis.document = previousDocument;
        if (previousStorage === undefined) delete globalThis.sessionStorage;
        else globalThis.sessionStorage = previousStorage;
    });
    const callbacks = [];
    return {
        values, inputs, callbacks,
        change: () => changed(),
        component: {
            invokeMethodAsync: async (name, ...args) => {
                callbacks.push([name, ...args]);
                if (name === "RestoreDraft") {
                    ["display-name", "description", "pronouns"].forEach((id, index) => inputs.get(id).value = args[index + 1]);
                }
            }
        }
    };
}

test("input is retained before expiry, restored for the same account, and keeps its original CID", async (t) => {
    const b = browser(t);
    const first = await attach("did:plc:first", "original-cid", b.component);
    b.inputs.get("display-name").value = "Unsaved name";
    b.inputs.get("description").value = "Unsaved description";
    b.change();
    first.dispose();
    b.inputs.get("display-name").value = "Current name";
    b.inputs.get("description").value = "";
    const restored = await attach("did:plc:first", "new-server-cid", b.component);
    assert.deepEqual(b.callbacks.at(-1), ["RestoreDraft", "original-cid", "Unsaved name", "Unsaved description", ""]);
    assert.equal(b.inputs.get("display-name").value, "Unsaved name");
    b.inputs.get("pronouns").value = "they/them";
    b.change();
    assert.equal(JSON.parse(b.values.get("bluesky-profile-draft:did:plc:first")).cid, "original-cid");
    await restored.reset("saved-cid", "Unsaved name", "Unsaved description", "they/them");
    assert.equal(b.values.size, 0);
    b.change();
    assert.equal(b.values.size, 0);
    restored.dispose();
});

test("a different account cannot restore another account's draft, and discard clears only its own draft", async (t) => {
    const b = browser(t);
    b.values.set("bluesky-profile-draft:did:plc:first", JSON.stringify({ cid: "first-cid", values: ["Private draft", "", ""] }));
    const second = await attach("did:plc:second", "second-cid", b.component);
    assert.equal(b.callbacks.length, 0);
    b.inputs.get("display-name").value = "Second draft";
    b.change();
    await second.clear();
    assert.ok(b.values.has("bluesky-profile-draft:did:plc:first"));
    assert.equal(b.values.size, 1);
    second.dispose();
});

test("storage failures are explicitly surfaced", async (t) => {
    const b = browser(t);
    t.mock.method(console, "error", () => {});
    globalThis.sessionStorage.getItem = () => { throw new Error("storage unavailable"); };
    const draft = await attach("did:plc:first", "cid", b.component);
    assert.deepEqual(b.callbacks.at(-1), ["DraftStorageFailed"]);
    draft.dispose();
});
