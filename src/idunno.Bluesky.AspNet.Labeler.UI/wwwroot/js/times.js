// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

(() => {
    "use strict";
    const key = "xenon.timeZone";
    document.querySelectorAll("[data-date-times]").forEach(section => {
        const button = section.querySelector("[data-time-zone-toggle]");
        const zone = section.querySelector("[data-time-zone]");
        const controls = section.querySelector("[data-time-controls]");
        const status = section.querySelector("[data-time-status]");
        if (!button || !zone) return;
        const formatter = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "long" });
        const relativeFormatter = new Intl.RelativeTimeFormat(undefined, { numeric: "always" });
        const relativeText = date => {
            const seconds = (date.getTime() - Date.now()) / 1000;
            if (Math.abs(seconds) < 60) return seconds > 0 ? "In a few seconds" : "Just now";
            for (const [unit, duration] of [
                ["year", 31536000], ["month", 2592000], ["week", 604800],
                ["day", 86400], ["hour", 3600], ["minute", 60]
            ]) {
                if (Math.abs(seconds) >= duration) {
                    return relativeFormatter.format(Math.trunc(seconds / duration), unit);
                }
            }
        };
        const times = Array.from(section.querySelectorAll("time[datetime]"), time => ({
            element: time,
            utc: time.textContent,
            date: new Date(time.dateTime),
            relative: time.hasAttribute("data-relative-time")
        })).filter(time => !Number.isNaN(time.date.getTime()));
        times.filter(time => time.relative).forEach((time, index) => {
            time.label = document.createElement("span");
            time.tooltip = document.createElement("span");
            time.tooltip.className = "identity-tooltip";
            time.tooltip.id = `relative-time-${index}`;
            time.tooltip.setAttribute("role", "tooltip");
            time.element.classList.add("identity");
            time.element.tabIndex = 0;
            time.element.setAttribute("aria-describedby", time.tooltip.id);
            time.element.replaceChildren(time.label, time.tooltip);
        });
        if (controls) controls.hidden = false;
        button.hidden = false;
        const apply = local => {
            times.forEach(time => {
                const exact = local ? formatter.format(time.date) : time.utc;
                if (time.relative) {
                    time.label.textContent = relativeText(time.date);
                    time.tooltip.textContent = exact;
                } else {
                    time.element.textContent = exact;
                }
            });
            zone.textContent = local ? "Local" : "UTC";
            button.setAttribute("aria-pressed", String(local));
            const action = local ? "Switch displayed dates to UTC" : "Switch displayed dates to your local time zone";
            button.setAttribute("aria-label", action);
            button.title = `${action}.`;
        };
        if (times.some(time => time.relative)) {
            setInterval(() => {
                times.filter(time => time.relative).forEach(time => {
                    time.label.textContent = relativeText(time.date);
                });
            }, 60000);
        }
        const storageError = () => {
            if (status) status.textContent = "Your browser could not save or read the time zone preference. Changes apply to this page only.";
        };
        apply(false);
        try {
            apply(localStorage.getItem(key) === "local");
        } catch (error) {
            console.warn("Xenon could not read the saved time zone preference.", error);
            storageError();
        }
        button.addEventListener("click", () => {
            const local = button.getAttribute("aria-pressed") !== "true";
            apply(local);
            try {
                localStorage.setItem(key, local ? "local" : "utc");
                if (status) status.textContent = "";
            } catch (error) {
                console.warn("Xenon could not save the time zone preference.", error);
                storageError();
            }
        });
    });
})();
