// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

document.querySelectorAll('select[name="appealOutcome"]').forEach(outcome => {
    const form = outcome.closest("form");
    const labels = Array.from(form.querySelectorAll('input[name="selectedLabels"]:not(:disabled)'));
    let suggested = false;
    let previous = outcome.value;

    outcome.addEventListener("change", () => {
        suggested = false;
        previous = outcome.value;
    });

    labels.forEach(label => label.addEventListener("change", () => {
        const changed = labels.some(input =>
            input.checked !== (input.closest("[data-applied]").dataset.applied === "true"));
        if (changed) {
            if (!suggested) previous = outcome.value;
            outcome.value = labels.some(input => input.checked) ? "Replaced" : "Withdrawn";
            suggested = true;
        } else if (suggested) {
            outcome.value = previous;
            suggested = false;
        }
    }));
});
