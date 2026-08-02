#!/usr/bin/env node

import { createRequire } from "node:module";
import { readdirSync, readFileSync } from "node:fs";
import { relative, resolve } from "node:path";

const requireFromFrontend = createRequire(
  new URL("../../frontend/package.json", import.meta.url)
);
const { load } = requireFromFrontend("js-yaml");

const contracts = resolve(process.argv[2] ?? "specs/contracts");
const problems = [];
let sharedDocument = null;

const yamlFiles = (directory) =>
  readdirSync(directory, { withFileTypes: true })
    .flatMap((entry) => {
      const path = resolve(directory, entry.name);
      return entry.isDirectory()
        ? yamlFiles(path)
        : entry.isFile() && entry.name.endsWith(".yaml")
          ? [path]
          : [];
    })
    .sort();

const findInt64Formats = (node, path = "$", seen = new WeakSet()) => {
  if (node === null || typeof node !== "object") return [];
  if (seen.has(node)) return [];
  seen.add(node);

  if (Array.isArray(node)) {
    return node.flatMap((value, index) =>
      findInt64Formats(value, `${path}[${index}]`, seen)
    );
  }

  return Object.entries(node).flatMap(([key, value]) => {
    const childPath = `${path}.${key}`;
    const here = key === "format" && value === "int64" ? [childPath] : [];
    return here.concat(findInt64Formats(value, childPath, seen));
  });
};

for (const file of yamlFiles(contracts)) {
  const name = relative(contracts, file);
  try {
    const document = load(readFileSync(file, "utf8"), { filename: name });
    if (name === "shared.yaml") sharedDocument = document;
    for (const path of findInt64Formats(document)) {
      problems.push(
        `${name}:${path}: \`format: int64\` в публичном контракте — ` +
          "замените схему на $ref '../shared.yaml#/components/schemas/Int64String'"
      );
    }
  } catch (error) {
    problems.push(`${name}: YAML не разобран: ${error.message}`);
  }
}

const schema = sharedDocument?.components?.schemas?.Int64String ?? null;
process.stdout.write(JSON.stringify({ problems, schema }));
