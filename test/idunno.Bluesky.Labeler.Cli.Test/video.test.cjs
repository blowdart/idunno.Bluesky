// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const { resolve } = require("node:path");
const { test } = require("node:test");
const { runInNewContext } = require("node:vm");
const script = readFileSync(resolve(__dirname, "../../src/idunno.Bluesky.AspNet.Labeler.UI/wwwroot/js/video.js"), "utf8");

function player(dimensions = {}) {
    const callbacks = {};
    const state = { src: null, modal: false, paused: 0, loaded: 0, focused: false };
    const open = {
        dataset: { videoUrl: "/Xenon/Media?token=protected", ...dimensions },
        addEventListener(name, callback) { callbacks.open = callback; },
        focus() { state.focused = true; }
    };
    const close = { addEventListener(name, callback) { callbacks.close = callback; } };
    const dialog = {
        showModal() { state.modal = true; },
        close() { state.modal = false; callbacks.closed(); },
        addEventListener(name, callback) { callbacks.closed = callback; }
    };
    const video = {
        style: {},
        videoWidth: 0,
        videoHeight: 0,
        set src(value) { state.src = value; },
        pause() { state.paused++; },
        removeAttribute(name) { assert.equal(name, "src"); state.src = null; },
        load() { state.loaded++; },
        addEventListener(name, callback) { callbacks[name] = callback; }
    };
    const error = { hidden: true };
    const viewport = { innerWidth: 1200, innerHeight: 800, addEventListener(name, callback) { callbacks.viewportResize = callback; } };
    runInNewContext(script, {
        window: viewport,
        document: {
            querySelectorAll() {
                return [{
                    querySelector(selector) {
                        return {
                            "[data-video-open]": open, "[data-video-close]": close,
                            dialog, video, "[data-video-error]": error
                        }[selector];
                    }
                }];
            }
        }
    });
    return { callbacks, state, error, video, viewport, dialog };
}

test("Video is not requested until its placeholder opens the modal", () => {
    const { state, callbacks } = player();
    assert.equal(state.src, null);
    assert.equal(state.modal, false);
    callbacks.open();
    assert.equal(state.modal, true);
    assert.equal(state.src, "/Xenon/Media?token=protected");
});

test("Closing releases the video and restores focus, including native Escape closure", () => {
    for (const nativeClose of [false, true]) {
        const { state, callbacks } = player();
        callbacks.open();
        if (nativeClose) callbacks.closed();
        else callbacks.close();
        assert.equal(state.src, null);
        assert.equal(state.paused, 1);
        assert.equal(state.loaded, 1);
        assert.equal(state.focused, true);
    }
});

test("Media errors are visible and reset on reopening", () => {
    const { callbacks, error } = player();
    callbacks.open();
    callbacks.error();
    assert.equal(error.hidden, false);
    callbacks.close();
    callbacks.open();
    assert.equal(error.hidden, true);
});

test("Declared portrait and landscape ratios size the player before the media request", () => {
    for (const [width, height] of [[9, 16], [16, 9], [1, 1]]) {
        const { callbacks, video } = player({ videoWidth: String(width), videoHeight: String(height) });
        callbacks.open();
        const renderedWidth = Number.parseFloat(video.style.width);
        assert.equal(Number(video.style.aspectRatio), width / height);
        assert.ok(renderedWidth <= 960);
        assert.ok(renderedWidth / (width / height) <= 608);
    }
});

test("Actual metadata overrides declared ratio and viewport changes refit an open player", () => {
    const { callbacks, video, viewport, dialog } = player({ videoWidth: "16", videoHeight: "9" });
    callbacks.open();
    video.videoWidth = 1080;
    video.videoHeight = 1920;
    callbacks.loadedmetadata();
    assert.equal(Number(video.style.aspectRatio), 1080 / 1920);
    viewport.innerHeight = 400;
    viewport.innerWidth = 320;
    dialog.open = true;
    callbacks.viewportResize();
    const width = Number.parseFloat(video.style.width);
    assert.ok(width <= 224);
    assert.ok(width / (1080 / 1920) <= 248);
});

test("Missing or invalid dimensions use a finite fallback until metadata arrives", () => {
    for (const dimensions of [{}, { videoWidth: "-1", videoHeight: "0" }, { videoWidth: "NaN", videoHeight: "16" }]) {
        const { callbacks, video } = player(dimensions);
        callbacks.open();
        assert.equal(Number(video.style.aspectRatio), 16 / 9);
        assert.ok(Number.isFinite(Number.parseFloat(video.style.width)));
    }
});
