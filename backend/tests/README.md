# Find backend tests by functionality

Open `backend/IoBuild.sln` in Rider, expand the test project, then navigate
**Context → Capability/Journey → Verification layer**. Only folders with actual
tests are present. Risk tiers A–D remain metadata, not directory buckets.

## Functionality navigation

| Context | Start here | Catalogue |
|---|---|---|
| IAM | `Modules/IAM/{Registration,Login,Logout,AuthorizedAccess,AccountLifecycle,Dispatch}` | [IAM](../../docs/bounded-contexts/iam/test-catalog.md) |
| Profiles | `Modules/Profiles/{Creation,Management,Photo}` | [Profiles](../../docs/bounded-contexts/profiles/test-catalog.md) |
| Publishing | `Modules/Publishing/{Management,Structure,Units,Clients}` | [Publishing](../../docs/bounded-contexts/publishing/test-catalog.md) |
| Subscriptions | `Modules/Subscriptions/{Purchase,Webhooks,Plans,StripeAdapter}` | [Subscriptions](../../docs/bounded-contexts/subscriptions/test-catalog.md) |
| Devices | `Modules/Devices/{Management,Control,Telemetry}` | [Devices](../../docs/bounded-contexts/devices/test-catalog.md) |
| Analytics | `Modules/Analytics/{Access,Dashboard,ReadQueries,LiveReads}` | [Analytics](../../docs/bounded-contexts/analytics/test-catalog.md) |

For service collaboration tests, also inspect
`Integration/Analytics/ReadModels/Application` and
`Integration/Devices/CommandTelemetry/Application`.
`Integration/CrossContext/Cutover/Application` owns ordered imports,
backup/restore, freeze/switch and stabilization across contexts.

## Projects and verification boundaries

| Existing project | Responsibility |
|---|---|
| `IoBuild.Modules.Tests` | Context-owned rules, application workflows, in-process API journeys, adapter contracts and opt-in MySQL proofs |
| `IoBuild.Integration.Tests` | Service collaboration/read models and cross-context cutover; its project name does **not** imply live infrastructure |
| `IoBuild.Contract.Tests` | Edge/configuration source contracts, readiness and characterized route catalogue; retain the `Contract/` role |
| `IoBuild.Architecture.Tests` | Assembly/module boundaries, compose/deployment cleanup and repository conventions; retain the `Architecture/` role |
| `IoBuild.TestKit` | Shared builders, traits and MySQL fixture; helper library, **not** a discovered test suite |

Layer folders describe the actual verification boundary:

- `Unit`: isolated parsing, mapping or configuration decisions.
- `Application`: direct service/workflow calls with controlled collaborators.
- `Api`: HTTP through an in-process application host, normally EF InMemory.
- `Contract`: adapter serialization/parsing using fake HTTP responses.
- `Persistence`: opt-in MySQL proofs or explicitly labelled EF model inspection.

An API response-contract test can retain `Layer=Contract` inside `Api/`.
Inherited traits are unchanged, including older adapter tests labelled
`Application`/`Api` and a password-hasher test labelled `Application`. The
catalogues report the boundary and inherited metadata separately.

**Dependency distinctions:** InMemory does not prove relational constraints,
transactions, collation, locking or migration behavior. Fake HTTP proves a
versioned adapter expectation, not a live Stripe, Cloudinary or InfluxDB
provider. MQTT-disabled/fake-publisher journeys do not prove broker delivery.
Opt-in MySQL methods can return early when `IOBUILD_TEST_MYSQL_CONNECTION` is
absent; xUnit reports those as passed, not skipped. The dispatch probe also
returns early if the shared outbox already contains rows. Do not interpret the
green default run as production-engine evidence.

## Run and filter

Run from the repository root:

```sh
dotnet test backend/IoBuild.sln --no-restore --verbosity minimal
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Context=Profiles"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Flow=IAM.LOGIN&Risk=A"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Dependency=MySql"
```

The last command requires a dedicated migrated MySQL test database supplied
through `IOBUILD_TEST_MYSQL_CONNECTION` to execute its assertions. The legacy
dispatch probe has no inherited `Dependency` trait; select it explicitly using
`FullyQualifiedName~MySql_dispatch_rows_survive_storage_and_recover_an_expired_lease`.
Existing runtime proof scripts and `fake_stripe_server.py` remain directly under
`Integration/` because their paths are entry points, not test classifications.

Use a catalogue's method/class filter for an exact scenario. `Context` is added
consistently to all Modules and Integration test classes; `Capability`, `Layer`
and `Dependency` are added to newly separated homogeneous classes where the
boundary is established. No new `Flow` identifiers or risk classifications
were inferred from names.

## Compatibility and risk preservation

Project/assembly names and all test method names, theory inputs, expectations,
original method traits and Flow identifiers are preserved. Most standalone
classes use folder-matching namespaces. Existing class names remain usable in
`FullyQualifiedName~ClassName` filters. Some splits use partial declarations to
retain the original class grouping, private fixtures and xUnit serialization;
all parts necessarily share a namespace, including the previously global IAM
B/C/D classes. Rider's file tree is the capability navigation in these cases.
`AnalyticsInfluxReadTests` also retains its original namespace: the production
live-energy service has a process-static cooldown, so changing test identity
changes runner ordering and can contaminate its success case after an outage.

`CoreBusinessWorkflowTests` and `PublishingAndSubscriptionsDddTests` are replaced
by cohesive classes; use the exact [move/split map](reorganization-map.md) to
translate those two old class selections.

Baseline and final source inventories contain **233 test methods**:
75 Tier A, 15 Tier B, 9 Tier C, 19 Tier D and 115 uncategorized.
Uncategorized means there was no risk trait; it does not mean happy-path-only,
low risk or missing assertions. Theory expansion yields **241 discovered cases**
(17 Architecture, 20 Contract, 42 Integration, 162 Modules).

See [local reorganization evidence](reorganization-evidence.md) for verification
commands, limitations and CI gate status.
