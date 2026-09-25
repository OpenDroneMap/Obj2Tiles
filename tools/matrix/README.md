# PICT option matrix

The quality-gate suite (`Obj2Tiles.Test/Quality/`) executes the CLI over a
pairwise-covering set of option combinations produced by Microsoft PICT.

## Files

| File | Role |
|------|------|
| `model.pict` | Parameter/computer definition. Single source of truth for the option space. |
| `generate.py` | Runs PICT, translates parameters into CLI arguments, self-checks the constraints, writes the committed matrix fixture. |
| `../validator/` | Official CesiumGS validator harness used by the tests (see its README). |

The generated fixture is committed at
`Obj2Tiles.Test/TestData/quality/matrix.json` so tests are reproducible without
PICT installed. Regeneration is only needed when `model.pict` changes.

## Regenerating

```sh
python3 generate.py --pict /path/to/pict
# or
export OBJ2TILES_PICT=/path/to/pict && python3 generate.py
```

Requirements: Python 3 (stdlib only) and the `pict` executable.

## Getting PICT

- **Windows:** download a release binary from
  <https://github.com/microsoft/pict/releases>.
- **Linux/macOS:** build from source:

```sh
git clone https://github.com/microsoft/pict /tmp/pict-src
cmake -S /tmp/pict-src -B /tmp/pict-build
cmake --build /tmp/pict-build
# binary ends up at /tmp/pict-build/cli/pict
```

## Model rules of thumb

- Values are quoted (`"Tiling"`); numeric values comparators use must stay unquoted
  (`[kthreads] = 0`, not `"0"`) or the constraints silently never match.
- The generator asserts the constraints on its side too: if PICT ever emits a case
  violating a constraint, generation fails instead of committing a broken matrix.
- `--scale` values must be plain decimal numbers: the CLI option parser rejects
  fractional syntax like `1200/3937`.
- Every row of the matrix must pass: CLI exit code 0, the structural inspector
  (`Obj2Tiles.Test/Quality/TilesetInspector.cs`) and the official validator harness
  (`tools/validator/validate.cjs` with `allowlist.json`).

## Reviewing the current matrix

```sh
python3 - <<'EOF'
import json
m = json.load(open('../../Obj2Tiles.Test/TestData/quality/matrix.json'))
print(len(m['cases']), 'cases')
for c in m['cases']:
    print(c['id'], c['dataset'], ' '.join(c['args']))
EOF
```
