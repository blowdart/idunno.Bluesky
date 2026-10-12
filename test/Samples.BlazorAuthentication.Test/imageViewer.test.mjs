// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

// Run the shipped module against the small part of the DOM it uses, without extra dependencies.
const source = await readFile(new URL("../../samples/Samples.BlazorAuthentication/wwwroot/imageViewer.js", import.meta.url), "utf8");

test("delegated image clicks open only their own dialog, including newly rendered images", async t => {
    const previousDocument = globalThis.document;
    let click;
    globalThis.document = {
        addEventListener: (event, callback) => {
            assert.equal(event, "click");
            click = callback;
        }
    };
    t.after(() => {
        if (previousDocument === undefined) delete globalThis.document;
        else globalThis.document = previousDocument;
    });
    await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);

    function image() {
        let opens = 0;
        const dialog = {
            open: false,
            showModal: () => { opens++; dialog.open = true; }
        };
        const viewer = {
            querySelector: selector => {
                assert.equal(selector, "dialog");
                return dialog;
            }
        };
        const button = {
            closest: selector => {
                assert.equal(selector, ".post-image-viewer");
                return viewer;
            }
        };
        const target = {
            closest: selector => {
                assert.equal(selector, "button[data-open-image]");
                return button;
            }
        };
        return { dialog, target, opens: () => opens };
    }

    const first = image();
    click({ target: first.target });
    assert.equal(first.opens(), 1);
    click({ target: first.target });
    assert.equal(first.opens(), 1);
    // Native dialog close (via form or Escape) allows the same thumbnail to open it again.
    first.dialog.open = false;
    click({ target: first.target });
    assert.equal(first.opens(), 2);

    const second = image();
    click({ target: second.target });
    assert.equal(second.opens(), 1);
    assert.equal(first.opens(), 2);
    click({ target: { closest: () => null } });
    click({ target: {} });
    assert.equal(second.opens(), 1);
});
