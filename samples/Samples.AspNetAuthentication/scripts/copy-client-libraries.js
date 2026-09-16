const fs = require("node:fs");
const path = require("node:path");

const projectDirectory = path.resolve(__dirname, "..");
const nodeModulesDirectory = path.join(projectDirectory, "node_modules");
const outputDirectory = path.join(projectDirectory, "wwwroot", "lib");

const libraries = [
    {
        name: "bootstrap",
        files: ["dist"]
    },
    {
        name: "jquery",
        files: ["dist"]
    },
    {
        name: "jquery-validation",
        files: ["dist/jquery.validate.js", "dist/jquery.validate.min.js"]
    },
    {
        name: "jquery-validation-unobtrusive",
        files: ["dist/jquery.validate.unobtrusive.js", "dist/jquery.validate.unobtrusive.min.js"]
    }
];

fs.rmSync(outputDirectory, { recursive: true, force: true });

for (const library of libraries) {
    const sourceDirectory = path.join(nodeModulesDirectory, library.name);
    const destinationDirectory = path.join(outputDirectory, library.name);

    for (const file of library.files) {
        const source = path.join(sourceDirectory, file);
        const destination = path.join(destinationDirectory, file);
        fs.mkdirSync(path.dirname(destination), { recursive: true });
        fs.cpSync(source, destination, { recursive: true });
    }
}
