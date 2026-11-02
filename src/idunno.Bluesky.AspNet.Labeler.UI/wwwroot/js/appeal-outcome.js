// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

document.querySelectorAll('select[name="appealOutcome"]').forEach(outcome => {
    const form = outcome.closest("form");
    const labels = Array.from(form.querySelectorAll('input[name="selectedLabels"]:not(:disabled)'));
    const appeal = form.querySelector('select[name="appealId"]');
    const explanation = form.querySelector("[data-appeal-changed]");
    const submit = form.querySelector('button[type="submit"]');

    function update(selectOutcome) {
        const changed = labels.some(input =>
            input.checked !== (input.closest("[data-applied]").dataset.applied === "true"));
        const obsolete = appeal.selectedOptions[0]?.dataset.changed === "true";
        explanation.hidden = !obsolete;
        submit.disabled = obsolete && changed;
        if (obsolete) {
            outcome.value = "Superseded";
        } else if (selectOutcome) {
            const suggested = changed
                ? (labels.some(input => input.checked) ? "Replaced" : "Withdrawn")
                : "Upheld";
            outcome.value = Array.from(outcome.options).some(option => option.value === suggested) ? suggested : "";
        }
    }

    labels.forEach(label => label.addEventListener("change", () => update(true)));
    appeal.addEventListener("change", () => update(true));
    outcome.addEventListener("change", () => update(false));
    update(false);
});
