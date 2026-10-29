// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

document.querySelectorAll("[data-team-change]").forEach(form => {
    const action = form.querySelector('select[name="action"]');
    const role = form.querySelector("[data-team-role]");
    const update = () => { role.hidden = action.value !== "SetRole"; };
    action.addEventListener("change", update);
    update();
});
