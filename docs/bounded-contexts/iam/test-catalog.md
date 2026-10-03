# IAM test catalogue

Navigate `backend/tests/Modules/IAM/` by the capability below. `U` means inherited
coverage with no risk trait; no A–D classification was invented during the move.
Counts below are methods, not theory-expanded cases.

| Capability | Relative file under `Modules/IAM/` | Actual layer / dependencies | Inherited tiers |
|---|---|---|---|
| Login rejection | `Login/Api/LoginCriticalTests.cs` | API, InMemory | A ×2 |
| Login hostile input/startup | `Login/Api/LoginExploratoryTests.cs` | API, InMemory; unreachable MySQL port for startup test | D ×2 |
| Blank login | `Login/Application/LoginDegradationTests.cs` | Application, InMemory | B |
| Oversized login | `Login/Application/LoginBoundaryTests.cs` | Application, InMemory | C |
| Hash verification | `Login/Application/LoginHashTests.cs` | Isolated hasher; inherited `Layer=Application` | D |
| Registration critical/error contracts | `Registration/Api/RegistrationCriticalTests.cs` | API, InMemory; error test samples login/access too | A ×3, B |
| Registration fuzz/bursts | `Registration/Api/RegistrationExploratoryTests.cs` | API, InMemory | D ×6 |
| Normalization | `Registration/Application/RegistrationDegradationTests.cs` | Application, InMemory | B |
| Normalization/sequential duplicates | `Registration/Application/RegistrationBoundaryTests.cs` | Application, InMemory | C ×2 |
| Normalization/guard campaigns | `Registration/Application/RegistrationExploratoryTests.cs` | Application, InMemory | D ×2 |
| Unique email model intent | `Registration/Persistence/RegistrationModelTests.cs` | EF model inspection, InMemory; **not** SQL uniqueness proof | C |
| Logout rejection/repetition | `Logout/Api/LogoutDegradationTests.cs` | API, InMemory | B ×2 |
| Expired revocation | `Logout/Application/LogoutDegradationTests.cs` | Application, InMemory | B |
| Token authorization | `AuthorizedAccess/Api/AuthorizedAccessCriticalTests.cs` | API, InMemory | A ×2 |
| Malformed authorization | `AuthorizedAccess/Api/AuthorizedAccessExploratoryTests.cs` | API, InMemory | D ×2 |
| Registration→login→logout, Owner eligibility, Admin directory, invitation privacy | `AccountLifecycle/Api/IamApiContractTests.cs` | API, InMemory; signup eligibility theory has two routes | A ×5 |
| Registration, Owner linking, dispatch recovery and revocation | `AccountLifecycle/Application/IamWorkflowTests.cs` | Application, InMemory; linking spans Publishing/Devices/Analytics projections | A ×4, U ×7 |
| Duplicate/rollback/revocation/race/migration | `AccountLifecycle/Persistence/IamPersistenceMySqlTests.cs` | Persistence, opt-in MySQL | A ×5, C ×2 |
| Dispatch lease recovery | `Dispatch/Persistence/IamDispatchMySqlTests.cs` | Persistence, opt-in MySQL and initially empty outbox | U |

## Demonstrate a functionality

```sh
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Flow=IAM.LOGIN"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~IamApiContractTests"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~Registration_auto_links"
```

All IAM module classes support `Context=IAM`. The original `IamTierATests`
namespace and global B/C/D class identities are retained using partial classes;
open the capability file rather than relying on the old tier class name in
Rider. `IAM.REGISTRATION`, `IAM.LOGIN`, `IAM.LOGOUT` and
`IAM.AUTHORIZED_ACCESS` remain the inherited Flow values. Dispatch and some
older linking scenarios have only their inherited Category, plus Context.

`Contract/ReadinessContractTests.cs` also contains
`LegacyContractCharacterizationTests.Catalog_preserves_characterized_auth_routes_and_outcomes`:
route catalogue inspection, U, not a real HTTP login journey. Cross-context
cutover/import tests live in `Integration/CrossContext/Cutover/Application/`.

InMemory registration/dispatch evidence does not prove transaction rollback,
unique-index enforcement or MySQL collation. The production-engine methods
return early without the dedicated connection; the dispatch method additionally
returns early for a nonempty outbox. [Shared evidence and limits](../../../backend/tests/reorganization-evidence.md).
