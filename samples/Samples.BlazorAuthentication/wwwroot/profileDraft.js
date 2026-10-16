// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

// Capture edits in the browser, not just the circuit: expiry can remove the editor and sign-in
// navigates away from it. Per-tab, per-account storage lets the same account resume after sign-in
// without exposing a draft to a different account or putting OAuth credentials in browser storage.
export async function attach(did, cid, component) {
    const form = document.getElementById("profile-editor");
    const key = `bluesky-profile-draft:${did}`;
    const fields = ["display-name", "description", "pronouns"];
    let baseline = fields.map(id => document.getElementById(id).value);
    let version = cid;

    async function storageFailure(error) {
        console.error("Profile draft storage failed.", error);
        try {
            await component.invokeMethodAsync("DraftStorageFailed");
        } catch (notificationError) {
            console.error("Could not notify the disconnected editor about draft storage.", notificationError);
        }
    }

    try {
        const stored = sessionStorage.getItem(key);
        if (stored !== null) {
            const draft = JSON.parse(stored);
            if (typeof draft.cid !== "string" || !Array.isArray(draft.values) ||
                draft.values.length !== 3 || draft.values.some(value => typeof value !== "string")) {
                throw new Error("The saved profile draft is invalid.");
            }
            version = draft.cid;
            // Keep the draft's original CID, rather than the freshly loaded profile's CID. The
            // server's conditional-save check must still detect edits made elsewhere while away.
            await component.invokeMethodAsync("RestoreDraft", version, ...draft.values);
        }
    } catch (error) {
        await storageFailure(error);
    }

    function changed() {
        // Read DOM input immediately, including text not yet committed by Blazor's change binding.
        // This also preserves typing during a temporary circuit disconnection.
        const values = fields.map(id => document.getElementById(id).value);
        try {
            if (version === cid && values.every((value, index) => value === baseline[index])) {
                sessionStorage.removeItem(key);
            } else {
                sessionStorage.setItem(key, JSON.stringify({ cid: version, values }));
            }
        } catch (error) {
            void storageFailure(error);
        }
    }

    form.addEventListener("input", changed);
    return {
        async clear() {
            try {
                sessionStorage.removeItem(key);
            } catch (error) {
                await storageFailure(error);
            }
        },
        async reset(newCid, ...values) {
            // A successful save or explicit discard establishes a new baseline. Merely restoring
            // a draft must not clear it, since it hasn't been saved to the account yet.
            cid = newCid;
            version = newCid;
            baseline = values;
            await this.clear();
        },
        dispose() {
            // Detach from the old editor without deleting the draft: component removal on expiry
            // is precisely when the next authenticated editor will need to restore it.
            form.removeEventListener("input", changed);
        }
    };
}
