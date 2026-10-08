#!/usr/bin/env node
"use strict";

/**
 * Obj2Tiles quality-gate harness around the official CesiumGS 3D Tiles validator.
 *
 * Usage:
 *   node validate.cjs <tileset.json | output.3tz> [--allowlist <file>] [--report <file.json>] [--json]
 *
 * Exit codes:
 *   0  valid: no ERROR issues and every WARNING is listed in the allowlist
 *   1  quality-gate violation (an ERROR, or a WARNING that is not allowlisted)
 *   2  harness error (bad arguments, missing file, missing npm dependencies, validator crash)
 *
 * The allowlist file is a JSON array of { "type": "<ISSUE_TYPE>", "reason": "<justification>" }
 * entries (see allowlist.json). Allowlisting a WARNING is a reviewed decision: every entry
 * must carry a reason, and entries should reference the Obj2Tiles option or spec nuance that
 * makes the warning expected.
 */

const fs = require("fs");
const path = require("path");
const { read: readKtx2 } = require("ktx-parse");

const EXIT_OK = 0;
const EXIT_VIOLATION = 1;
const EXIT_HARNESS = 2;

function failHarness(message) {
  console.error(`[validator-harness] ${message}`);
  process.exit(EXIT_HARNESS);
}

function parseArgs(argv) {
  const args = { target: null, allowlist: path.join(__dirname, "allowlist.json"), report: null, json: false };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--allowlist") args.allowlist = argv[++i];
    else if (arg === "--report") args.report = argv[++i];
    else if (arg === "--json") args.json = true;
    else if (!arg.startsWith("--") && !args.target) args.target = arg;
    else failHarness(`Unexpected argument: ${arg}`);
  }
  if (!args.target) failHarness("Missing <tileset.json|output.3tz> argument");
  return args;
}

function loadValidator() {
  try {
    return require("3d-tiles-validator");
  } catch (e) {
    failHarness(
      `Cannot load '3d-tiles-validator' (${e.message}). Run 'npm ci' inside ${__dirname} first, ` +
      `or set OBJ2TILES_SKIP_NPM_INSTALL=1 to disable the automatic install.`,
    );
  }
}

function loadAllowlist(file) {
  if (!fs.existsSync(file)) {
    failHarness(`Allowlist file not found: ${file}`);
  }
  let parsed;
  try {
    parsed = JSON.parse(fs.readFileSync(file, "utf8"));
  } catch (e) {
    failHarness(`Allowlist file is not valid JSON (${file}): ${e.message}`);
  }
  if (!Array.isArray(parsed)) failHarness(`Allowlist file must contain a JSON array: ${file}`);
  const entries = [];
  for (const entry of parsed) {
    if (typeof entry?.type !== "string" || typeof entry?.reason !== "string" || !entry.reason.trim()) {
      failHarness(`Allowlist entry must have a non-empty 'type' and 'reason': ${JSON.stringify(entry)}`);
    }
    if (entry.messageIncludes !== undefined && typeof entry.messageIncludes !== "string") {
      failHarness(`Allowlist entry 'messageIncludes' must be a string: ${JSON.stringify(entry)}`);
    }
    entries.push(entry);
  }
  return {
    find(issueType, message) {
      for (const entry of entries) {
        if (entry.type !== issueType) continue;
        if (entry.messageIncludes && !String(message ?? "").includes(entry.messageIncludes)) continue;
        return entry.reason;
      }
      return undefined;
    },
  };
}

/**
 * The tileset validator collapses every per-content glTF finding into a single
 * CONTENT_VALIDATION_WARNING without details. We re-validate each flagged content
 * payload (b3dm -> embedded GLB, or raw GLB) with the bundled gltf-validator package
 * so allowlisting can happen at the granularity of individual glTF issue codes
 * (a broken accessor is not noise; a broken image is).
 *
 * The bundled gltf-validator (2.0.0-dev, pre-KTX2) rejects the image/ktx2 mimeType
 * (VALUE_NOT_IN_LIST) and cannot sniff KTX2 payloads (IMAGE_UNRECOGNIZED_FORMAT). Those two
 * findings are NOT allowlisted: each one is attributed to its image through the issue's JSON
 * pointer and dropped only when that exact image is a KTX2 container ktx-parse can read.
 * Any other image keeps its warning, so a truncated JPEG/PNG still fails the gate.
 */

const KTX2_IDENTIFIER = Buffer.from([0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a]);

function startsWithKtx2Magic(data) {
  return data.length >= KTX2_IDENTIFIER.length && data.subarray(0, KTX2_IDENTIFIER.length).equals(KTX2_IDENTIFIER);
}

// Returns null for a structurally sound KTX2 container, an error string otherwise.
function verifyKtx2Container(data) {
  if (!startsWithKtx2Magic(data)) return "payload is not a KTX2 container";
  let container;
  try {
    container = readKtx2(new Uint8Array(data));
  } catch (e) {
    return `KTX2 container unreadable (${e.message})`;
  }
  if (!(container.pixelWidth > 0 && container.pixelHeight > 0)) return "KTX2 has no pixel dimensions";
  if (container.levels.length < 1) return "KTX2 has no mip levels";
  if (container.levels.some((l) => l.levelData.byteLength === 0)) return "KTX2 has an empty mip level";
  if (!container.dataFormatDescriptor.some((d) => d.samples.length > 0)) return "KTX2 data format descriptor has no samples";
  return null;
}

function imageIndexFromPointer(pointer) {
  const match = /^\/images\/(\d+)(\/|$)/.exec(pointer ?? "");
  return match ? Number(match[1]) : null;
}

function imagePayload(glbJson, glbBin, index) {
  const image = Array.isArray(glbJson.images) ? glbJson.images[index] : null;
  if (!image || typeof image.bufferView !== "number") return null;
  const view = (glbJson.bufferViews || [])[image.bufferView];
  if (!view) return null;
  const offset = view.byteOffset || 0;
  const length = view.byteLength || 0;
  return length > 0 && offset + length <= glbBin.length ? glbBin.subarray(offset, offset + length) : null;
}

function parseGlbChunks(glb) {
  if (glb.length < 20 || glb.subarray(0, 4).toString("latin1") !== "glTF") return null;
  let off = 12;
  let json = null;
  let bin = Buffer.alloc(0);
  while (off + 8 <= glb.length) {
    const len = glb.readUInt32LE(off);
    const type = glb.readUInt32LE(off + 4);
    const start = off + 8;
    if (len < 0 || start + len > glb.length) break;
    if (type === 0x4e4f534a) {
      try { json = JSON.parse(glb.subarray(start, start + len).toString("utf8").replace(/\0+$/, "")); }
      catch { return null; }
    } else if (type === 0x004e4942) {
      bin = glb.subarray(start, start + len);
    }
    off = start + len + ((4 - (len % 4)) % 4);
  }
  return json ? { json, bin } : null;
}

async function drillContentIssues(target, validator, contentPaths) {
  const gltfValidator = require("gltf-validator");
  const isPackage = path.extname(target).toLowerCase() === ".3tz" ||
    path.extname(target).toLowerCase() === ".3dtiles";

  let readEntry;
  if (isPackage) {
    const StreamZip = require("node-stream-zip");
    const zip = new StreamZip.async({ file: target });
    const names = new Set(Object.keys(await zip.entries()));
    readEntry = async (rel) => {
      const clean = rel.split("/").filter((s) => s && s !== ".").join("/");
      const candidate = [...names].find(
        (n) => n === clean || n.endsWith("/" + clean) || clean.endsWith("/" + n));
      return candidate ? await zip.entryData(candidate) : null;
    };
    var zipClose = () => zip.close();
  } else {
    const baseDir = path.dirname(target);
    readEntry = async (rel) => {
      const full = path.resolve(baseDir, rel);
      if (!full.startsWith(path.resolve(baseDir) + path.sep)) return null; // traversal guard
      return fs.existsSync(full) ? fs.readFileSync(full) : null;
    };
    var zipClose = async () => {};
  }

  const severityNames = ["ERROR", "WARNING", "INFO", "HINT"];
  const records = [];
  const verified = [];
  const seen = new Set();
  const failed = new Set();

  try {
    for (const contentPath of contentPaths) {
      const bytes = await readEntry(contentPath);
      if (!bytes) { failed.add(contentPath); continue; }

      let glb = null;
      const magic = bytes.subarray(0, 4).toString("latin1");
      if (magic === "b3dm") {
        const total = bytes.readUInt32LE(8);
        // Obj2Tiles writes the GLB after the feature-table lengths; the second candidate
        // also skips a (currently always-empty) batch table for spec-shaped b3dms.
        const candidates = [
          28 + bytes.readUInt32LE(12) + bytes.readUInt32LE(16),
          28 + bytes.readUInt32LE(12) + bytes.readUInt32LE(16) + bytes.readUInt32LE(20) + bytes.readUInt32LE(24),
        ];
        const start = candidates.find((c) =>
          c + 12 <= bytes.length && bytes.subarray(c, c + 4).toString("latin1") === "glTF");
        if (start == null) { failed.add(contentPath); continue; }
        glb = bytes.subarray(start, Math.min(total || bytes.length, bytes.length));
      } else if (magic === "glTF") {
        glb = bytes;
      } else {
        // pnts/iTGB/etc.: no gltf-validator equivalent, keep the collapsed warning as-is.
        failed.add(contentPath);
        continue;
      }

      let report;
      try {
        report = await gltfValidator.validateBytes(new Uint8Array(glb), { maxIssues: 250 });
      } catch (e) {
        failed.add(contentPath);
        continue;
      }
      const out = [];
      for (const issue of report?.issues?.messages ?? []) {
        const severity = severityNames[issue.severity];
        if (severity === "INFO" || severity === "HINT") continue;
        const type = `GLTF_${issue.code}`;
        const message = `[${contentPath}] ${issue.message}`;
        const key = `${severity}|${type}|${issue.pointer ?? ""}|${message}`;
        if (seen.has(key)) continue;
        seen.add(key);
        out.push({ type, message, path: contentPath, severity, pointer: issue.pointer ?? "" });
      }

      // Drop a blind-spot finding only when its own image is a readable KTX2 container.
      const blindSpot = (r) =>
        (r.type === "GLTF_VALUE_NOT_IN_LIST" && r.message.includes("image/ktx2")) ||
        r.type === "GLTF_IMAGE_UNRECOGNIZED_FORMAT";
      const chunks = out.some(blindSpot) ? parseGlbChunks(Buffer.from(glb)) : null;
      for (const r of out.filter(blindSpot)) {
        const index = imageIndexFromPointer(r.pointer);
        const data = chunks !== null && index !== null ? imagePayload(chunks.json, chunks.bin, index) : null;
        const problem = data === null ? "image payload not found" : verifyKtx2Container(data);
        if (problem === null) {
          verified.push({ ...r, verification: "ktx2-container-verified" });
          out.splice(out.indexOf(r), 1);
        } else {
          r.message += ` (not a valid KTX2 image: ${problem})`;
        }
      }

      records.push(...out);
    }
  } finally {
    await zipClose();
  }

  return { records, verified, failed, drilled: contentPaths.filter((p) => !failed.has(p)) };
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const target = path.resolve(args.target);
  if (!fs.existsSync(target)) failHarness(`Target does not exist: ${target}`);

  const allowlist = loadAllowlist(args.allowlist);
  const { Validators, ValidationIssueSeverity } = loadValidator();
  if (typeof Validators?.validateTilesetFile !== "function") {
    failHarness("Unexpected 3d-tiles-validator API: Validators.validateTilesetFile is missing");
  }

  // validateTilesetFile dispatches on the file extension: .json tilesets are validated
  // directly, .3tz/.3dtiles package files are unpacked and validated internally.
  const result = await Validators.validateTilesetFile(target);

  // Expand the collapsed per-content warnings via the bundled gltf-validator before
  // classification, so the allowlist can act on individual glTF issue codes.
  const collapsedContentWarnings = result.issues.filter(
    (i) => i.type === "CONTENT_VALIDATION_WARNING" && i.severity === ValidationIssueSeverity.WARNING);
  const contentPaths = [...new Set(collapsedContentWarnings.map((i) => String(i.path ?? "")).filter(Boolean))];

  let drill = { records: [], verified: [], drilled: [] };
  if (contentPaths.length > 0) {
    try {
      drill = await drillContentIssues(target, Validators, contentPaths);
    } catch (e) {
      console.error(`[validator-harness] content drill-down failed (${e.message}); keeping collapsed warnings`);
    }
  }
  const drilledSet = new Set(drill.drilled);

  const errors = [];
  const warnings = [];
  const infos = [];
  const warningsAllowed = [];

  const classify = (record) => {
    if (record.severity === ValidationIssueSeverity.ERROR) errors.push(record);
    else if (record.severity === ValidationIssueSeverity.WARNING) {
      const reason = allowlist.find(record.type, record.message ?? "");
      if (reason !== undefined) warningsAllowed.push({ ...record, reason });
      else warnings.push(record);
    } else infos.push(record);
  };

  for (const issue of result.issues) {
    if (issue.type === "CONTENT_VALIDATION_WARNING" && drilledSet.has(String(issue.path ?? ""))) {
      continue; // replaced by the drill-down's granular GLTF_* findings
    }
    classify({
      severity: issue.severity,
      type: issue.type,
      message: issue.message,
      path: Array.isArray(issue.path) ? issue.path.join("/") : String(issue.path ?? ""),
    });
  }

  for (const record of drill.records) classify(record);

  const report = {
    target,
    validatorVersion: require("3d-tiles-validator/package.json").version,
    allowlistFile: path.resolve(args.allowlist),
    passed: errors.length === 0 && warnings.length === 0,
    counts: {
      errors: errors.length,
      warningsUnallowlisted: warnings.length,
      warningsAllowlisted: warningsAllowed.length,
      verifiedContentIssues: drill.verified.length,
      infos: infos.length,
    },
    errors,
    warningsUnallowlisted: warnings,
    warningsAllowlisted: warningsAllowed,
    verifiedContentIssues: drill.verified,
    infos,
  };

  if (args.report) {
    fs.mkdirSync(path.dirname(path.resolve(args.report)), { recursive: true });
    fs.writeFileSync(args.report, JSON.stringify(report, null, 2));
  }

  if (args.json) {
    console.log(JSON.stringify(report, null, 2));
  } else {
    console.log(`[validator] ${path.relative(process.cwd(), target) || target}: ` +
      `${errors.length} error(s), ${warnings.length} unallowlisted warning(s), ` +
      `${warningsAllowed.length} allowlisted warning(s), ${infos.length} info(s)`);
    for (const e of errors) console.error(`  ERROR  ${e.type}  ${e.path}  ${e.message}`);
    for (const w of warnings) console.error(`  WARN   ${w.type}  ${w.path}  ${w.message}`);
    for (const w of warningsAllowed) console.log(`  WARN*  ${w.type}  ${w.path}  (allowlisted: ${w.reason})`);
    if (drill.verified.length > 0)
      console.log(`  OK-KTX2 ${drill.verified.length} bundled-validator KTX2 blind-spot finding(s) replaced by active KTX2 container checks`);
  }

  process.exit(report.passed ? EXIT_OK : EXIT_VIOLATION);
}

main().catch((e) => failHarness(`Validator crashed: ${e.stack || e.message}`));
