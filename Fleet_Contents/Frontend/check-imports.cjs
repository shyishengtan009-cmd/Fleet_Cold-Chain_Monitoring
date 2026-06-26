const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "src");
const missing = [];

function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full);
    else if (/\.(vue|ts)$/.test(entry.name)) checkFile(full);
  }
}

function resolves(resolved) {
  const candidates = [
    resolved,
    resolved + ".ts",
    resolved + ".vue",
    resolved + ".tsx",
    path.join(resolved, "index.ts")
  ];
  return candidates.some((c) => fs.existsSync(c));
}

function checkFile(file) {
  const content = fs.readFileSync(file, "utf-8");
  const re = /from\s+["'](@\/[^"']+|[./][^"']+)["']/g;
  let m;
  while ((m = re.exec(content))) {
    const imp = m[1];
    const resolved = imp.startsWith("@/")
      ? path.resolve(root, imp.slice(2))
      : path.resolve(path.dirname(file), imp);
    if (!resolves(resolved)) {
      missing.push(`${path.relative(root, file)} -> ${imp}`);
    }
  }
}

walk(root);
console.log(missing.length ? missing.join("\n") : "No missing relative imports.");
