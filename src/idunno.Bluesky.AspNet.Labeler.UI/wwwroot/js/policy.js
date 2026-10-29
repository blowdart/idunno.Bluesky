// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

"use strict";

function parsePolicyDeclaration(text) {
    const value = JSON.parse(text);
    const object = item => item !== null && typeof item === "object" && !Array.isArray(item);
    const strings = items => Array.isArray(items) && items.every(item => typeof item === "string");
    if (!object(value) || !object(value.policies) || !strings(value.policies.labelValues) ||
        !strings(value.subjectTypes) || value.subjectTypes.some(item => !["account", "record"].includes(item)) ||
        ("reasonTypes" in value && !strings(value.reasonTypes)) ||
        ("subjectCollections" in value && !strings(value.subjectCollections))) {
        throw new Error("The declaration needs policies.labelValues and subjectTypes arrays, with string arrays for optional reasonTypes and subjectCollections.");
    }
    if ("labelValueDefinitions" in value.policies &&
        (!Array.isArray(value.policies.labelValueDefinitions) || value.policies.labelValueDefinitions.some(item =>
            !object(item) || typeof item.identifier !== "string" ||
            ["severity", "blurs", "defaultSetting"].some(key => key in item && typeof item[key] !== "string") ||
            ("adultOnly" in item && typeof item.adultOnly !== "boolean") ||
            ("locales" in item && (!Array.isArray(item.locales) || item.locales.some(locale =>
                !object(locale) || ["lang", "name", "description"].some(key => typeof locale[key] !== "string"))))))) {
        throw new Error("Label definitions must be objects with an identifier, text metadata and valid locale entries.");
    }
    const identifiers = (value.policies.labelValueDefinitions ?? []).map(item => item.identifier);
    if (new Set(value.policies.labelValues).size !== value.policies.labelValues.length ||
        new Set(identifiers).size !== identifiers.length || identifiers.some(item => !value.policies.labelValues.includes(item))) {
        throw new Error("Label values and definition identifiers must be unique; every definition must have a declared label value.");
    }
    return value;
}

function setPolicyReportScope(declaration, subjects, collections, reasons) {
    declaration.subjectTypes = subjects;
    if (collections === null) delete declaration.subjectCollections;
    else declaration.subjectCollections = collections;
    if (reasons === null) delete declaration.reasonTypes;
    else declaration.reasonTypes = reasons;
    return declaration;
}

document.querySelectorAll("[data-policy-tabs]").forEach(root => {
    const tabs = Array.from(root.querySelectorAll("[data-policy-tab]"));
    const panels = Array.from(root.querySelectorAll("[data-policy-panel]"));
    const save = root.querySelector("[data-policy-save]");
    const nav = root.querySelector("nav");
    nav.setAttribute("role", "tablist");
    const show = tab => {
        tabs.forEach(item => {
            const selected = item === tab;
            item.setAttribute("aria-selected", String(selected));
            item.tabIndex = selected ? 0 : -1;
        });
        panels.forEach(panel => { panel.hidden = "#" + panel.id !== tab.getAttribute("href"); });
        const editing = ["#policy-labels", "#policy-moderation", "#policy-advanced"].includes(tab.getAttribute("href"));
        if (save) save.hidden = !editing;
        const toolbar = root.querySelector(".policy-editor-toolbar");
        if (toolbar) toolbar.hidden = !editing;
    };
    tabs.forEach((tab, index) => {
        const panel = root.querySelector(tab.getAttribute("href"));
        tab.id = "tab-" + panel.id;
        tab.setAttribute("role", "tab");
        tab.setAttribute("aria-controls", panel.id);
        panel.setAttribute("role", "tabpanel");
        panel.setAttribute("aria-labelledby", tab.id);
        panel.tabIndex = 0;
        tab.addEventListener("click", event => {
            event.preventDefault();
            show(tab);
        });
        tab.addEventListener("keydown", event => {
            let next;
            if (event.key === "ArrowRight") next = (index + 1) % tabs.length;
            else if (event.key === "ArrowLeft") next = (index + tabs.length - 1) % tabs.length;
            else if (event.key === "Home") next = 0;
            else if (event.key === "End") next = tabs.length - 1;
            else return;
            event.preventDefault();
            show(tabs[next]);
            tabs[next].focus();
        });
    });
    show(tabs.find(tab => tab.getAttribute("href") === location.hash) ||
        (root.dataset.policyVerificationSubmitted === "true" ? tabs.find(tab => tab.getAttribute("href") === "#policy-verification") : null) ||
        (root.dataset.policyPublicationSubmitted === "true" ? tabs.find(tab => tab.getAttribute("href") === "#policy-publication") : null) ||
        tabs[root.querySelector("[data-policy-editor]")?.dataset.policySubmitted === "true" ? 3 : 0]);
    root.addEventListener("invalid", event => {
        const panel = event.target.closest("[data-policy-panel]");
        if (panel) show(tabs.find(tab => tab.getAttribute("href") === "#" + panel.id));
    }, true);

    const form = root.querySelector("[data-policy-editor]");
    if (!form) return;
    const field = name => form.elements.namedItem(name);
    const raw = field("declaration");
    const error = root.querySelector("#policy-json-error");
    const labels = root.querySelector("[data-policy-labels]");
    const add = root.querySelector("[data-policy-add-label]");
    let declaration;
    const list = (value, separator) => value.split(separator).map(item => item.trim()).filter(Boolean);
    const write = () => {
        raw.value = JSON.stringify(declaration, null, 2);
        raw.setCustomValidity("");
        error.textContent = "";
    };
    const reportControls = () => {
        if (!declaration) return;
        const reasons = field("reasonMode").value === "all" ? null : [
            ...new Set([
                ...Array.from(form.querySelectorAll('input[name="reasonTypes"]:checked'), input => input.value),
                ...list(field("customReasons").value, /\r?\n/)
            ])
        ];
        setPolicyReportScope(declaration,
            Array.from(form.querySelectorAll('input[name="reportSubjects"]:checked'), input => input.value),
            field("reportAllCollections").checked ? null : list(field("reportCollections").value, ","),
            reasons);
        write();
    };
    const input = (parent, title, value, type = "text") => {
        const label = document.createElement("label");
        label.className = type === "checkbox" ? "field-check" : "field";
        const control = document.createElement(type === "textarea" ? "textarea" : "input");
        if (type !== "textarea") control.type = type;
        if (type === "checkbox") control.checked = value;
        else control.value = value ?? "";
        label.append(document.createTextNode(title), control);
        parent.append(label);
        return control;
    };
    const select = (parent, title, value, options) => {
        const label = document.createElement("label");
        label.className = "field";
        label.append(document.createTextNode(title));
        const control = document.createElement("select");
        if (!options.includes(value)) options = [...options, value];
        options.forEach(item => {
            const option = document.createElement("option");
            option.value = item;
            option.textContent = item;
            control.append(option);
        });
        control.value = value;
        label.append(control);
        parent.append(label);
        return control;
    };
    const button = (parent, title, action) => {
        const control = document.createElement("button");
        control.type = "button";
        control.textContent = title;
        control.addEventListener("click", action);
        parent.append(control);
        return control;
    };
    const renderLabels = () => {
        labels.replaceChildren();
        declaration.policies.labelValues.forEach((value, index) => {
            const original = declaration.policies.labelValueDefinitions?.find(item => item.identifier === value);
            const card = document.createElement("fieldset");
            card.className = "policy-label";
            const legend = document.createElement("legend");
            legend.textContent = value || "New label";
            card.append(legend);
            labels.append(card);
            const identifier = input(card, "Label value", value);
            identifier.maxLength = 128;
            const custom = input(card, "Custom label definition", Boolean(original), "checkbox");
            const metadata = document.createElement("div");
            metadata.hidden = !original;
            card.append(metadata);
            const severity = select(metadata, "Severity", original?.severity ?? "inform", ["inform", "alert", "none"]);
            const blurs = select(metadata, "Blur behaviour", original?.blurs ?? "none", ["none", "content", "media"]);
            const setting = select(metadata, "Default setting", original?.defaultSetting ?? "warn", ["ignore", "warn", "hide"]);
            const adult = input(metadata, "Adult content only", original?.adultOnly ?? false, "checkbox");
            const locales = document.createElement("div");
            metadata.append(locales);
            const localeValues = [];
            const update = () => {
                if (raw.validationMessage) return;
                const old = declaration.policies.labelValues[index];
                declaration.policies.labelValues[index] = identifier.value;
                let definitions = declaration.policies.labelValueDefinitions ?? [];
                definitions = definitions.filter(item => item.identifier !== old);
                if (custom.checked) {
                    definitions.push({
                        ...original,
                        identifier: identifier.value,
                        severity: severity.value,
                        blurs: blurs.value,
                        defaultSetting: setting.value,
                        adultOnly: adult.checked,
                        locales: localeValues.map(item => ({
                            ...item.original, lang: item.lang.value, name: item.name.value, description: item.description.value
                        }))
                    });
                }
                if (definitions.length || "labelValueDefinitions" in declaration.policies) {
                    declaration.policies.labelValueDefinitions = definitions;
                }
                metadata.hidden = !custom.checked;
                legend.textContent = identifier.value || "New label";
                write();
            };
            const addLocale = (locale = { lang: "", name: "", description: "" }) => {
                const group = document.createElement("fieldset");
                const heading = document.createElement("legend");
                heading.textContent = "Translation";
                group.append(heading);
                locales.append(group);
                const entry = {
                    original: locale,
                    lang: input(group, "Language tag", locale.lang),
                    name: input(group, "Name", locale.name),
                    description: input(group, "Description", locale.description, "textarea")
                };
                localeValues.push(entry);
                button(group, "Remove translation", () => {
                    localeValues.splice(localeValues.indexOf(entry), 1);
                    group.remove();
                    update();
                });
            };
            (original?.locales ?? []).forEach(addLocale);
            button(metadata, "Add translation", () => { addLocale(); update(); });
            button(card, "Remove label", () => {
                const removed = declaration.policies.labelValues.splice(index, 1)[0];
                if (declaration.policies.labelValueDefinitions) {
                    declaration.policies.labelValueDefinitions = declaration.policies.labelValueDefinitions.filter(item => item.identifier !== removed);
                }
                write();
                renderLabels();
            });
            card.addEventListener("input", update);
            card.addEventListener("change", update);
        });
    };
    const reflect = () => {
        const known = Array.from(form.querySelectorAll('input[name="reasonTypes"]'));
        const reasons = declaration.reasonTypes;
        field("reasonMode").value = reasons === undefined ? "all" : "selected";
        known.forEach(control => { control.checked = reasons?.includes(control.value) ?? false; });
        field("customReasons").value = (reasons ?? []).filter(reason => !known.some(control => control.value === reason)).join("\n");
        form.querySelectorAll('input[name="reportSubjects"]').forEach(control => {
            control.checked = declaration.subjectTypes.includes(control.value);
        });
        field("reportCollections").value = (declaration.subjectCollections ?? []).join(", ");
        field("reportAllCollections").checked = declaration.subjectCollections === undefined;
        renderLabels();
    };
    const read = () => {
        let parsed;
        try {
            parsed = parsePolicyDeclaration(raw.value);
        } catch (exception) {
            error.textContent = "Declaration not synchronized: " + exception.message + " Structured controls retain the last valid values. Correct the JSON before saving.";
            raw.setCustomValidity("Correct the declaration JSON before saving.");
            add.disabled = true;
            form.querySelectorAll("[data-policy-declaration-controls], .policy-label").forEach(control => {
                control.disabled = true;
            });
            return;
        }
        declaration = parsed;
        error.textContent = "";
        raw.setCustomValidity("");
        reflect();
        add.disabled = false;
        form.querySelectorAll("[data-policy-declaration-controls], .policy-label").forEach(control => {
            control.disabled = false;
        });
    };
    raw.addEventListener("input", read);
    ["reasonMode", "customReasons", "reportCollections"].forEach(name => {
        field(name).addEventListener("input", () => { if (!raw.validationMessage) reportControls(); });
    });
    form.querySelectorAll('input[name="reasonTypes"], input[name="reportSubjects"], input[name="reportAllCollections"]').forEach(control => {
        control.addEventListener("change", () => { if (!raw.validationMessage) reportControls(); });
    });
    add.hidden = false;
    add.addEventListener("click", () => {
        declaration.policies.labelValues.push("");
        write();
        renderLabels();
        labels.lastElementChild.querySelector("input").focus();
    });
    const load = (definition, source) => {
        raw.value = JSON.stringify(definition.declaration, null, 2);
        field("allowAccounts").checked = definition.allowAccounts;
        field("allowRecords").checked = definition.allowRecords;
        field("collections").value = (definition.allowedRecordCollections ?? []).join(", ");
        field("issueAllCollections").checked = definition.allowedRecordCollections == null;
        field("rules").value = definition.rules;
        field("version").value = "";
        field("reason").value = "";
        root.querySelector("[data-policy-source]").textContent = source;
        read();
        show(tabs[1]);
    };
    root.querySelectorAll("[data-policy-load]").forEach(link => {
        const text = root.querySelector(link.dataset.policyLoad === "draft" ? "[data-policy-drafts]" : "[data-policy-current]").value;
        if (!text) return;
        link.addEventListener("click", event => {
            event.preventDefault();
            const parsed = JSON.parse(text);
            const definition = link.dataset.policyLoad === "draft" ? parsed.find(item => item.version === link.dataset.policyVersion) : parsed;
            load(definition, (link.dataset.policyLoad === "draft" ? "Saved draft: " : "Current policy: ") + definition.version);
        });
    });
    root.querySelector("[data-policy-clear]").addEventListener("click", event => {
        event.preventDefault();
        load({
            declaration: {
                $type: "app.bsky.labeler.service", createdAt: new Date().toISOString(),
                subjectTypes: [], policies: { labelValues: [] }
            },
            allowAccounts: false, allowRecords: false, rules: ""
        }, "New policy");
    });
    read();
});
