# Analytics test catalogue

Navigate `backend/tests/Modules/Analytics/` for endpoint/live-read boundaries
and `backend/tests/Integration/Analytics/ReadModels/Application/` for direct
read-model collaboration. Counts are methods; `U` means no inherited Risk.

| Capability | File relative to `backend/tests/` | Actual layer / dependencies | Inherited tiers |
|---|---|---|---|
| Self-scoped metrics/energy and project-related insights | `Modules/Analytics/Access/Api/AnalyticsAccessTests.cs` | API, InMemory | A ×2 |
| Builder/Owner dashboard data and energy-window clamp | `Modules/Analytics/Dashboard/Api/AnalyticsDashboardTests.cs` | API, InMemory; dashboard methods retain `Layer=Contract` | A ×2, B |
| Dashboard/projection durability and concurrent sync | `Modules/Analytics/Dashboard/Persistence/AnalyticsDashboardMySqlTests.cs` | Persistence, opt-in MySQL | A, C |
| Insights determinism, error bodies and read fuzz | `Modules/Analytics/ReadQueries/Api/AnalyticsReadRiskTests.cs` | API, InMemory | A, B, D |
| Flux CSV annotations, headers and quoted fields | `Modules/Analytics/LiveReads/Unit/FluxCsvTests.cs` | Unit, literal CSV | A, B |
| Energy/status CSV mapping and degradation | `Modules/Analytics/LiveReads/Contract/AnalyticsInfluxReadTests.cs` | Adapter contract, fake HTTP | A ×2, C |
| Builder/Owner dashboard empty/seeded data, status/temperature, energy and projection importer/LWW | `Integration/Analytics/ReadModels/Application/AnalyticsTests.cs` | Application, InMemory, fake live-energy/status collaborators | U ×19 |

## Run the functionality

```sh
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Context=Analytics"
dotnet test backend/tests/Integration/IoBuild.Integration.Tests.csproj --no-restore --filter "Context=Analytics"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Flow=ANALYTICS.LIVE"
```

Inherited Flow values `ANALYTICS.VIEW` and `ANALYTICS.LIVE` are preserved. The
Integration read-model suite uses Analytics services and projections even when
it seeds Publishing/Devices rows for self-sync: ownership remains Analytics.
The separate cutover harness owns ordered multi-context import.

Dashboard and live-read partial declarations retain the class selection and
private seed/CSV helpers; their namespaces are shared across layer folders.
`AnalyticsInfluxReadTests` specifically retains `IoBuild.Modules.Tests` because
its original runner order depends on the identity while the production adapter
has a static 60-second failure cooldown. This inherited order sensitivity is
documented, not solved by weakening expectations or changing production code.
`AnalyticsTiersTests` retains its class name inside the capability-named file.
Fake Influx HTTP is a parser/mapping proof, not live query availability, Flux
execution or authentication evidence. InMemory cannot prove SQL sync races or
migrations. MySQL methods return early without the dedicated connection.
[Local verification and limits](../../../backend/tests/reorganization-evidence.md).
