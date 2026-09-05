# Dev server implementation verification

## Stage 1 — 2026-09-05

Branch: `dev-server`, from `main` at `75300ac`.

- Loader builds with zero warnings/errors; baseline mod tests pass 498/498.
- Removed `BepInEx/plugins/ScriptEngine.dll` and `BepInEx/scripts` from the owner's
  Songs of Conquest installation, as requested by the migration plan.
- Direct launch of `SongsOfConquest.exe` works. Loopback server answers on port 8772.
- `soc-access/tests/dev-server-stage1.ps1` passed all checks against the running game:
  missing mod survives; `1+1` returns `2`; Unity reports `2022.3.67f2`; scene hierarchy,
  PNG signature, log ring, one-second false predicate, empty-body reload, unknown query
  rejection, and bodyless POST returning 411.
- Used the reference repository's net35 mcs binary unchanged; provenance is in
  `vendor/mcs/NOTICE`. No compiler fallback was needed.
- The mod has deliberately not yet been deployed into the new loader folder.
