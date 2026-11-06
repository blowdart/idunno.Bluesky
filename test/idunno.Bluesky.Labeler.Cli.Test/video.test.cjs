// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/video.js"), "utf8");

test("Inline players surface errors without starting playback or requesting media from JavaScript", () => {
    let failed;
    const error = { hidden: true };
    const video = {
        addEventListener(name, callback) { assert.equal(name, "error"); failed = callback; },
        play() { assert.fail("Autoplay is not allowed"); },
        load() { assert.fail("Media must not be loaded eagerly"); },
        set src(value) { assert.fail("The native inline player owns loading"); }
    };
    runInNewContext(script, {
        document: {
            querySelectorAll(selector) {
                assert.equal(selector, ".video-preview");
                return [{ querySelector: name => name === "video" ? video : error }];
            }
        }
    });
    assert.equal(error.hidden, true);
    failed();
    assert.equal(error.hidden, false);
});
