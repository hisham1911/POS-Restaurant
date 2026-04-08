import fs from "node:fs";
import path from "node:path";
import ts from "typescript";

const projectRoot = process.cwd();
const srcDir = path.join(projectRoot, "src");
const targetExtensions = new Set([".ts", ".tsx"]);
const ignoreSuffixes = [".d.ts"];

function collectSourceFiles(directory, files = []) {
  const entries = fs.readdirSync(directory, { withFileTypes: true });

  for (const entry of entries) {
    const absolutePath = path.join(directory, entry.name);

    if (entry.isDirectory()) {
      collectSourceFiles(absolutePath, files);
      continue;
    }

    const extension = path.extname(entry.name);
    if (!targetExtensions.has(extension)) {
      continue;
    }

    if (ignoreSuffixes.some((suffix) => entry.name.endsWith(suffix))) {
      continue;
    }

    files.push(absolutePath);
  }

  return files;
}

function checkFileForAnyTypes(filePath) {
  const sourceText = fs.readFileSync(filePath, "utf8");
  const scriptKind = filePath.endsWith(".tsx")
    ? ts.ScriptKind.TSX
    : ts.ScriptKind.TS;

  const sourceFile = ts.createSourceFile(
    filePath,
    sourceText,
    ts.ScriptTarget.Latest,
    true,
    scriptKind,
  );

  const violations = [];

  function visit(node) {
    if (node.kind === ts.SyntaxKind.AnyKeyword) {
      const { line, character } = sourceFile.getLineAndCharacterOfPosition(
        node.getStart(sourceFile),
      );

      violations.push({
        filePath,
        line: line + 1,
        column: character + 1,
      });
    }

    ts.forEachChild(node, visit);
  }

  visit(sourceFile);
  return violations;
}

if (!fs.existsSync(srcDir)) {
  console.error("Source directory not found:", srcDir);
  process.exit(1);
}

const sourceFiles = collectSourceFiles(srcDir);
const allViolations = sourceFiles.flatMap((filePath) =>
  checkFileForAnyTypes(filePath),
);

if (allViolations.length > 0) {
  console.error("\nFound disallowed 'any' usage in frontend/src:\n");
  for (const violation of allViolations) {
    const relativePath = path.relative(projectRoot, violation.filePath);
    console.error(
      `${relativePath}:${violation.line}:${violation.column} -> any is not allowed`,
    );
  }

  console.error("\nUse specific types instead of 'any'.\n");
  process.exit(1);
}

console.log("No 'any' usage found in frontend/src.");
