#!/usr/bin/env python3
"""Generate the Obj2Tiles CLI quality-gate test matrix with PICT.

Reads tools/matrix/model.pict (PICT pairwise model), runs the PICT binary and
writes the committed fixture consumed by Obj2Tiles.Test:
    Obj2Tiles.Test/TestData/quality/matrix.json

PICT is deterministic for a given model + version, so matrix.json is reviewed
in code review like any other test asset. PICT binaries exist for Windows
(microsoft/pict releases); on Linux build from source (see README.md).

Usage:
    python3 tools/matrix/generate.py [--pict <path>] [--model <path>] [--out <path>]

The pict binary is located from: --pict, then $OBJ2TILES_PICT, then PATH.
"""

import argparse
import csv
import io
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
DEFAULT_MODEL = os.path.join(HERE, "model.pict")
DEFAULT_OUT = os.path.join(REPO, "Obj2Tiles.Test", "TestData", "quality", "matrix.json")

# Logical value -> emitted CLI fragment. Functions get the whole row for
# cross-parameter context.
BOOLEANS = {"zsplit", "octree", "noroot", "keeptex", "smp", "uastc", "yuptozup", "temp"}


def find_pict(explicit):
    for candidate in (explicit, os.environ.get("OBJ2TILES_PICT"), shutil.which("pict")):
        if candidate and os.path.isfile(candidate) and os.access(candidate, os.X_OK):
            return candidate
    sys.exit(
        "error: PICT binary not found. Pass --pict <path>, set OBJ2TILES_PICT, or put 'pict' on PATH.\n"
        "Windows: https://github.com/microsoft/pict/releases  |  Linux: see tools/matrix/README.md"
    )


def run_pict(pict, model):
    # /d:	 default; no /r -> deterministic generation, no seeding needed.
    proc = subprocess.run([pict, model], capture_output=True, text=True, check=False)
    if proc.returncode != 0:
        sys.exit(f"error: pict failed ({proc.returncode}):\n{proc.stderr}")
    return proc.stdout


def parse_pict_table(text):
    rows = list(csv.reader(io.StringIO(text), delimiter="\t"))
    rows = [r for r in rows if r and any(c.strip() for c in r)]
    header, *cases = rows
    return [h.strip() for h in header], [dict(zip([h.strip() for h in header], (c.strip() for c in row))) for row in cases]


def cli_args(row):
    """Map a PICT row to Obj2Tiles CLI arguments (excluding input/output)."""
    args = []

    def flag(name, value=None):
        args.append(f"--{name}")
        if value is not None:
            args.append(str(value))

    flag("stage", row["stage"])
    flag("lods", row["lods"])
    flag("divisions", row["divisions"])
    if row["zsplit"] == "true":
        flag("zsplit")
    flag("split-strategy", row["splitstrategy"])
    if row["octree"] == "true":
        flag("octree")
    if row["noroot"] == "true":
        flag("no-root-content")
    flag("error", row["error"])
    flag("texture-format", row["texfmt"])
    flag("texture-quality", row["texqual"])
    flag("max-texture-size", row["maxtex"])
    flag("lod-texture-scale", row["lodscale"])
    if row["keeptex"] == "true":
        flag("keeptextures")
    if row["smp"] == "true":
        flag("single-material-per-part")
    if row["uastc"] == "true":
        flag("ktx2-uastc")
    if row["kthreads"] != "0":
        flag("ktx2-threads", row["kthreads"])
    if row["zstd"] != "0":
        flag("ktx2-zstd-level", row["zstd"])
    if row["geo"] == "local":
        flag("local")
    else:
        flag("lat", "45.464242")
        flag("lon", "9.190277")
        flag("alt", "180")
    if row["yuptozup"] == "true":
        flag("y-up-to-z-up")
    if row["scale"] != "1":
        flag("scale", row["scale"])
    if row["outform"] == "3tz":
        flag("3tz")
    flag("3tz-compression", row["tzcomp"])
    if row["temp"] == "true":
        flag("use-system-temp")
    return args


def self_check(rows):
    """Re-check the constraints in python so a broken model cannot sneak in."""
    for i, r in enumerate(rows):
        if r["outform"] == "3tz" and r["stage"] != "Tiling":
            sys.exit(f"constraint violation in case {i}: outform=3tz with stage={r['stage']}")
        if r["outform"] == "dir" and r["tzcomp"] != "6":
            sys.exit(f"constraint violation in case {i}: outform=dir with tzcomp={r['tzcomp']}")
        if r["texfmt"] != "Ktx2" and (r["kthreads"] != "0" or r["zstd"] != "0" or r["uastc"] != "false"):
            sys.exit(f"constraint violation in case {i}: ktx2 knobs with texfmt={r['texfmt']}")
        if r["zstd"] != "0" and r["uastc"] != "true":
            sys.exit(f"constraint violation in case {i}: zstd={r['zstd']} with uastc={r['uastc']}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pict", help="Path to the pict binary")
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--out", default=DEFAULT_OUT)
    ns = parser.parse_args()

    pict = find_pict(ns.pict)
    header, rows = parse_pict_table(run_pict(pict, ns.model))

    logical = [p for p in header if p in BOOLEANS | {"stage", "lods", "divisions", "splitstrategy",
                                                     "error", "texfmt", "texqual", "maxtex", "lodscale",
                                                     "uastc", "kthreads", "zstd", "geo", "scale",
                                                     "outform", "tzcomp"}]
    unexpected = set(header) - set(logical)
    if unexpected:
        sys.exit(f"error: unknown PICT parameters {sorted(unexpected)} - update generate.py mapping")

    self_check(rows)

    cases = []
    for i, row in enumerate(rows):
        out_name = "out.3tz" if row["outform"] == "3tz" else "out"
        cases.append({
            "id": f"pict-{i:03d}",
            "dataset": "cube-textured",
            "args": cli_args(row),
            "outform": row["outform"],
            "outName": out_name,
            "params": {k: row[k] for k in header},
        })

    os.makedirs(os.path.dirname(ns.out), exist_ok=True)
    with open(ns.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"generator": os.path.basename(pict), "model": os.path.basename(ns.model),
                   "coverage": 2, "cases": cases}, f, indent=2)
        f.write("\n")

    print(f"Generated {len(cases)} cases -> {os.path.relpath(ns.out, REPO)}")


if __name__ == "__main__":
    main()
