# Backend test reorganization: local verification

**Outcome:** the same 241 cases are discovered before and after the approved
Context → Capability/Journey → Layer reorganization. All 233 source test
methods retain their exact bodies and original method attributes. Two final
full solution runs report 241/241 passed. This is **local organizational
acceptance**, not CI, live-provider or live-database acceptance.

Date: 2026-10-02. Runtime: .NET 9 / VSTest 17.13.0, Windows.
Work unit: backend test organization and navigation documentation only.
Journey: a developer finds a functionality in Rider, selects its existing
scenarios, and runs them without losing or duplicating coverage.
Actors: developer/reviewer; existing Builder, Owner, Admin and anonymous
application scenarios are preserved verbatim rather than extended.

## Acceptance and boundary ownership

| Observable criterion | Evidence |
|---|---|
| Same project boundaries | Four test csproj names and TestKit helper csproj unchanged |
| Same scenarios, inputs and assertions | Exact multiset comparison of 233 method names/bodies/original attributes against tracked HEAD baseline; zero changed/missing/extra methods |
| Same theory expansion | Project-by-project `--list-tests` comparison of method + argument identities; zero missing/extra cases |
| Functionality navigation | Six context catalogues, README and exact move/split map |
| Same risk metadata | 75 A, 15 B, 9 C, 19 D, 115 uncategorized source methods; original Flow/Layer/Dependency/Category/InlineData attributes retained |
| Source paths/filters remain usable | Ledger owner paths updated; removed mixed classes mapped explicitly; workflow still selects solution and `Risk=A`; runtime script paths retained |

Frontend, application implementation, persistence schema, API contracts and
external adapters are unchanged. Test harness imports, namespaces, class
declarations and shared-helper visibility change only to support organization.
Verification ownership is discovery + exact-source comparison for preservation,
and the existing coded suites for executable compatibility; no new behavioral
tests or empty coverage folders were added.

## Baseline and final inventory

| Project | Source methods | Baseline discovered | Final discovered | Baseline full run | Accepted full run | Deterministic rerun |
|---|---:|---:|---:|---:|---:|---:|
| Architecture | 17 | 17 | 17 | 17 passed | 17 passed | 17 passed |
| Contract | 20 | 20 | 20 | 20 passed | 20 passed | 20 passed |
| Integration | 42 | 42 | 42 | 42 passed | 42 passed | 42 passed |
| Modules | 154 | 162 | 162 | 162 passed | 162 passed | 162 passed |
| **Total** | **233** | **241** | **241** | **241 passed** | **241 passed** | **241 passed** |

TestKit is a helper library with no test SDK/xUnit suite; it builds through
Modules and has no independent discovered cases. Theory expansion adds eight
cases: one Owner route, one profile range and six plan-feature partitions.

The runner reported 0 failed and 0 skipped in both accepted full runs. Those
are runner counts: opt-in MySQL methods **return early** when the dedicated
connection is absent, so they must not be recorded as executed SQL proof.
`IOBUILD_TEST_MYSQL_CONNECTION` was absent in this verification environment.

Baseline durations by project: Architecture 1 s, Contract 1 s, Integration 2 s,
Modules 1 m 4 s. Accepted full run: 3 s / 1 s / 2 s / 59 s respectively.
Final rerun: 360 ms / 1 s / 1 s / 1 m respectively. These are VSTest project
durations, not overall wall-clock timings.

## Commands run

The discovery command was run separately for each of Architecture, Contract,
Integration and Modules, before edits and after edits:

```sh
dotnet test backend/tests/Architecture/IoBuild.Architecture.Tests.csproj --no-restore --list-tests --verbosity minimal
dotnet test backend/tests/Contract/IoBuild.Contract.Tests.csproj --no-restore --list-tests --verbosity minimal
dotnet test backend/tests/Integration/IoBuild.Integration.Tests.csproj --no-restore --list-tests --verbosity minimal
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --list-tests --verbosity minimal
dotnet test backend/IoBuild.sln --no-restore --verbosity minimal
```

The full-solution command ran at baseline, during diagnosis and twice after the
repair. Modules discovery was refreshed after restoring the Influx namespace.

Additional checks:

```sh
python "C:/Users/halli/AppData/Local/Temp/opencode/verify-backend-reorg.py"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~AnalyticsInfluxReadTests" --verbosity minimal
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --list-tests --filter "Risk=A" --verbosity minimal
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --list-tests --filter "Context=Profiles" --verbosity minimal
git diff --check
```

The read-only temporary verifier compares tracked baseline source with current
source and discovery output; it reports 233→233 exact methods and 241→241
expanded cases. It is an audit aid, not a replacement for versioned coded tests.
Profiles Context discovery selects 21 cases. Tier-A discovery confirms the
preserved selection (76 expanded cases). All accepted execution comes from
the repository's coded tests, without MCP/browser intervention.

## Failure diagnosis and repair

| Observation | Classification / evidence | Repair / outcome |
|---|---|---|
| Extracted Stripe tests/support failed to compile | Test harness imports: CS0246/CS0103; production and environment unchanged; CodeGraph resolves the declarations under Subscriptions application/infrastructure | Restore the required imports; no assertion/body changes; subsequent discovery and builds succeed |
| Renamed Influx test namespace: successful energy mapping expected 2 points, received 0 | Inherited test/production-state coupling. Focused class reproduces failure. `LiveEnergyService._lastFailureTime` is static; degradation establishes a 60-second cooldown. Pure CSV parser tests remain unchanged. New fully-qualified identity changes xUnit ordering | Retain `IoBuild.Modules.Tests.AnalyticsInfluxReadTests` identity across Contract/Unit partial files. Focused run 5/5; two full runs 241/241 |

No expectations were relaxed, retries used as unexplained acceptance, live
dependencies substituted for failed assertions, or product fixes introduced.
The final repair is compatibility preservation, **not** a general solution to
the inherited static cooldown/order sensitivity.

## Convergent gates and skipped evidence

Following `docs/delivery-discipline.md`, local results are recorded separately
from delivery gate status: without CI, local-only evidence does not close a
delivery gate as passed.

| Gate | Delivery status | Local evidence / reason |
|---|---|---|
| G0 Local | skipped | Existing unit/application/parser methods run in the full local suite; CI not executed for this change |
| G1 Boundaries | skipped | Existing in-process API and fake-HTTP contracts run locally; live MySQL opt-in methods return early; no CI/live-provider claim |
| G2 System | skipped | No changed user-visible behavior; frontend/full-stack E2E and runtime proof scripts are outside organization-only acceptance |
| G3 Risk | skipped | Original A–D assertions execute in the default local suite except opt-in database bodies; no new risk campaign or CI run |
| G4 Delivery | skipped | Two deterministic local full runs and source/discovery audits pass; CI delivery evidence pending |

## Open risks and follow-up ownership

| What | Why deferred | Who resumes | Review date |
|---|---|---|---|
| Isolate Influx cooldown state between test scenarios | Inherited order sensitivity is outside behavior-preserving file organization; original identity retained | ccarita-tech, backend test maintainer | 2026-10-09 |
| Run dedicated MySQL and CI acceptance | No dedicated test connection/CI execution in this session; early-return passes are not relational evidence | ccarita-tech, CI/backend maintainer | 2026-10-09 |

Fake HTTP/MQTT tests retain their established scope; no live-provider/broker
claim is made. Existing NU1902 and xUnit2031 warnings remain outside this work
unit. No unexplained failure remains in the final two full runs; this does not
establish order-independent correctness for the inherited Influx suite.

## Local artifacts

Logs remain under `C:/Users/halli/AppData/Local/Temp/opencode/`:

- `backend-reorg-before-{Architecture,Contract,Integration,Modules}.txt` and
  `backend-reorg-before-full.txt`: baseline discovery/execution.
- `backend-reorg-after-full.txt`, `backend-reorg-after-Modules.txt`: preserved
  initial compile failures.
- `backend-reorg-final-full.txt`, `backend-reorg-influx-diagnosis.txt`: preserved
  cooldown failure and focused reproduction.
- `backend-reorg-final-{Architecture,Contract,Integration,Modules}.txt`: accepted
  discovery (Modules refreshed after identity repair).
- `backend-reorg-influx-restored.txt`: 5/5 focused acceptance.
- `backend-reorg-accepted-full.txt`, `backend-reorg-accepted-rerun.txt`: two
  successful full runs.
- `backend-reorg-tier-a.txt`, `backend-reorg-context-profiles.txt`: filter discovery.
- `verify-backend-reorg.py`: read-only source/discovery audit aid.

These are local temporary artifacts, not published CI artifacts. The original
local appsettings edit and `.atl/`/`.codegraph/` directories were preserved.
No commit or push was made.
