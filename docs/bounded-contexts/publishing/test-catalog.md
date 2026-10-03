# Publishing test catalogue

Navigate `backend/tests/Modules/Publishing/`. `U` means inherited coverage with
no Risk trait; counts are test methods.

| Capability | Relative file under `Modules/Publishing/` | Layer / dependencies | Inherited tiers |
|---|---|---|---|
| Own projects, structure/units and clients | `Management/Api/PublishingAccessTests.cs` | API, InMemory; authorization and lifecycle journeys | A ×3 |
| Structure validation, error bodies and mixed unit/client payload campaign | `Management/Api/PublishingValidationRiskTests.cs` | API, InMemory | A, B, D |
| Legacy list/structure status sequence | `Structure/Api/ProjectStructureRouteTests.cs` | API, InMemory | U |
| Provision units plus floor/unit devices | `Structure/Application/ProjectStructureWorkflowTests.cs` | Application, InMemory; Publishing-owned provisioning spanning Devices | U |
| Structure determinism, ownership and duplicate/parallel definition | `Structure/Persistence/PublishingPersistenceMySqlTests.cs` | Persistence, opt-in MySQL | A ×3 |
| Unit creation/query and owner assignment/projection outcomes | `Units/Application/UnitCommandQueryTests.cs` | Application, InMemory; IAM owner lookup/projection collaborator | U ×4 |
| Unit constructor and ownership state transitions | `Units/Domain/UnitEntityTests.cs` | Domain, no dependencies | 3 methods / 5 cases (owner-clear theory has 3 inputs) |
| Client CRUD and client→unit→owner projection | `Clients/Application/ClientCommandQueryTests.cs` | Application, InMemory; IAM owner lookup | U ×2 |
| Client construction and detail/unit updates | `Clients/Domain/ClientEntityTests.cs` | Domain, no dependencies | 2 methods / 2 cases |

## Run the functionality

```sh
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Context=Publishing"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~UnitCommandQueryTests"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~ClientCommandQueryTests"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~UnitEntityTests"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~ClientEntityTests"
```

`PUBLISHING.MANAGE` remains the existing Flow. The validation campaign spans
units/clients/structure, so its owning capability is Management rather than a
misleading single-entity label. `PublishingTiersTests` retains its class filter
inside the capability-named file. DDD/CoreBusiness extractions are cohesive
classes with original Category and no speculative Risk/Flow.

The legacy method name ending in `atomically` is preserved, but its InMemory
assertions prove provisioned row counts, **not** SQL transaction atomicity.
Owner lookup/projection collaboration remains owned by the Publishing use case.
Ordered multi-context cutover belongs to `Integration/CrossContext/Cutover/`.
MySQL proofs return early without the dedicated connection.
[Local evidence and limits](../../../backend/tests/reorganization-evidence.md).
