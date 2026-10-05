# Profiles test catalogue

Navigate `backend/tests/Modules/Profiles/`. Counts are methods; `U` means no
inherited Risk trait. The service range theory expands to two cases.

| Capability | Relative file under `Modules/Profiles/` | Layer / dependencies | Inherited tiers |
|---|---|---|---|
| Creation fields and years-in-business range | `Creation/Application/ProfileCreationTests.cs` | Application, InMemory | U ×2 |
| Own create/read/update, age/years separation, photo route, errors/fuzz | `Management/Api/ProfileAccessTests.cs` | API, InMemory; injected photo uploader | A ×4, B ×3, D |
| Create/read/update, migration/backfill, duplicate create and photo durability | `Management/Persistence/ProfilePersistenceMySqlTests.cs` | Persistence, opt-in MySQL; fake uploader | A ×4 |
| Owner/Builder profile updates, success feedback, and error retention | `frontend/tests/e2e/profiles-manage.spec.js` | Playwright system E2E; clean MySQL stack | G2, both actors |
| Failed upload leaves stored photo unchanged | `Photo/Application/ProfilePhotoFailureTests.cs` | Application, InMemory; broken uploader | A |
| Photo success/failure/CAS | `Photo/Application/ProfilePhotoWorkflowTests.cs` | Application, InMemory; fake uploader | U ×3 |
| Signed multipart and provider failure | `Photo/Contract/CloudinaryHttpAdapterTests.cs` | Adapter contract, fake HTTP and fixed time | U ×2 |

## Run the functionality

```sh
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Context=Profiles"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~ProfileCreationTests"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudinaryHttpAdapterTests"
```

Existing `PROFILES.MANAGE` Flow identifiers remain unchanged. The extracted
CoreBusiness scenarios had no Flow/Risk; their new class-level metadata reports
Context, Capability, Layer and Dependency without inventing tiers. The
`ProfileAccessTests` partial in `Photo/Application` shares the Management API
namespace and private helpers to retain its class filter and runner grouping.

The fake HTTP contract does not establish Cloudinary availability or credentials.
InMemory does not establish relational durability; MySQL methods require
`IOBUILD_TEST_MYSQL_CONNECTION` and otherwise return early as xUnit passes.
IAM account linking and cross-context import are catalogued in IAM and the
backend entry point, rather than duplicated here.
[Local verification and remaining infrastructure gates](../../../backend/tests/reorganization-evidence.md).
