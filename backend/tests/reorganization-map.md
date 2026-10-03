# Exact backend test move and split map

All paths below are relative to `backend/tests/`. Method names, bodies,
theory inputs and original method traits are unchanged. Project files and
Contract/Architecture/TestKit source paths are retained; `TestKit/Traits.cs`
only gains Context/Capability constants. Runtime scripts/server paths under
`Integration/` are retained. This map is a migration record, not extra coverage.

## Moves and layer splits

| Original file | Destination(s) |
|---|---|
| `Modules/AnalyticsAccessTests.cs` | `Modules/Analytics/Access/Api/AnalyticsAccessTests.cs` |
| `Modules/AnalyticsDashboardTests.cs` | `Modules/Analytics/Dashboard/Api/AnalyticsDashboardTests.cs`; `Modules/Analytics/Dashboard/Persistence/AnalyticsDashboardMySqlTests.cs` (two MySQL methods) |
| `Modules/AnalyticsInfluxReadTests.cs` | `Modules/Analytics/LiveReads/Contract/AnalyticsInfluxReadTests.cs`; `Modules/Analytics/LiveReads/Unit/FluxCsvTests.cs` (two CSV parser methods) |
| `Modules/AnalyticsTiersTests.cs` | `Modules/Analytics/ReadQueries/Api/AnalyticsReadRiskTests.cs` |
| `Modules/DeviceControlFlowTests.cs` | `Modules/Devices/Control/Api/DeviceControlFlowTests.cs` |
| `Modules/DeviceControlMySqlTests.cs` | `Modules/Devices/Control/Persistence/DeviceControlMySqlTests.cs` |
| `Modules/DeviceManageTests.cs` | `Modules/Devices/Management/Api/DeviceManageTests.cs` |
| `Modules/DeviceTiersTests.cs` | `Modules/Devices/Control/Api/DeviceControlRiskTests.cs`; `Modules/Devices/Telemetry/Api/TelemetryPayloadRiskTests.cs` (telemetry fuzz) |
| `Modules/IamPersistenceMySqlTests.cs` | `Modules/IAM/AccountLifecycle/Persistence/IamPersistenceMySqlTests.cs` |
| `Modules/IamWorkflowTests.cs` | `Modules/IAM/AccountLifecycle/Application/IamWorkflowTests.cs`; `Modules/IAM/AccountLifecycle/Api/IamApiContractTests.cs` (existing API class); `Modules/IAM/Dispatch/Persistence/IamDispatchMySqlTests.cs` (dispatch MySQL method) |
| `Modules/ProfileAccessTests.cs` | `Modules/Profiles/Management/Api/ProfileAccessTests.cs`; `Modules/Profiles/Photo/Application/ProfilePhotoFailureTests.cs` (direct failed-upload workflow) |
| `Modules/ProfilePersistenceMySqlTests.cs` | `Modules/Profiles/Management/Persistence/ProfilePersistenceMySqlTests.cs` |
| `Modules/PublishingAccessTests.cs` | `Modules/Publishing/Management/Api/PublishingAccessTests.cs` |
| `Modules/PublishingPersistenceMySqlTests.cs` | `Modules/Publishing/Structure/Persistence/PublishingPersistenceMySqlTests.cs` |
| `Modules/PublishingTiersTests.cs` | `Modules/Publishing/Management/Api/PublishingValidationRiskTests.cs` |
| `Modules/StripeKeyDisciplineTests.cs` | `Modules/Subscriptions/StripeAdapter/Contract/StripeKeyDisciplineTests.cs`; `Modules/Subscriptions/StripeAdapter/Unit/StripeKeyResolverTests.cs` (three pure resolver methods) |
| `Modules/SubscriptionPersistenceMySqlTests.cs` | `Modules/Subscriptions/Purchase/Persistence/SubscriptionPersistenceMySqlTests.cs` |
| `Modules/SubscriptionPurchaseFlowTests.cs` | `Modules/Subscriptions/Purchase/Api/SubscriptionPurchaseFlowTests.cs`; `Modules/Subscriptions/Webhooks/Application/SubscriptionWebhookIdempotencyTests.cs` (direct processor method) |
| `Integration/AnalyticsTests.cs` | `Integration/Analytics/ReadModels/Application/AnalyticsTests.cs` |
| `Integration/IoTWorkflowTests.cs` | `Integration/Devices/CommandTelemetry/Application/IoTWorkflowTests.cs` (all three existing classes) |
| `Integration/CutoverTests.cs` | `Integration/CrossContext/Cutover/Application/CutoverTests.cs` |

## IAM risk files distributed by functionality

Existing IAM tier classes use partial declarations so every scenario keeps its
original class grouping and method Risk/Flow/Layer traits. Files are capability
cohorts, not new test copies.

| Original | Exact destination files (under `Modules/IAM/`) |
|---|---|
| `Modules/IamTierATests.cs` | `Login/Api/LoginCriticalTests.cs`; `AuthorizedAccess/Api/AuthorizedAccessCriticalTests.cs`; `Registration/Api/RegistrationCriticalTests.cs`; helper-only `TestSupport/IamTierATestSupport.cs` |
| `Modules/IamTierBTests.cs` | `Logout/Api/LogoutDegradationTests.cs`; `Login/Application/LoginDegradationTests.cs`; `Registration/Application/RegistrationDegradationTests.cs`; `Logout/Application/LogoutDegradationTests.cs` |
| `Modules/IamTierCTests.cs` | `Registration/Application/RegistrationBoundaryTests.cs`; `Registration/Persistence/RegistrationModelTests.cs`; `Login/Application/LoginBoundaryTests.cs` |
| `Modules/IamTierDTests.cs` | `Registration/Api/RegistrationExploratoryTests.cs`; `Registration/Application/RegistrationExploratoryTests.cs`; `AuthorizedAccess/Api/AuthorizedAccessExploratoryTests.cs`; `Login/Api/LoginExploratoryTests.cs`; `Login/Application/LoginHashTests.cs` |

## Mixed CoreBusiness class replaced

`Modules/CoreBusinessWorkflowTests.cs` becomes the following nine cohesive
classes/files. The 18 methods (19 discovered cases) occur exactly once.

| Destination (under `Modules/`) | Original scenarios |
|---|---|
| `Subscriptions/Webhooks/Application/StripeWebhookWorkflowTests.cs` | Invalid signature, duplicate signed event, legacy metadata |
| `Subscriptions/Webhooks/Api/StripeWebhookRouteTests.cs` | Unsigned HTTP callback |
| `Subscriptions/StripeAdapter/Unit/StripeOptionsTests.cs` | Restricted key/dynamic methods |
| `Subscriptions/StripeAdapter/Contract/StripeHttpAdapterTests.cs` | Checkout form/header contract, session/invoice retrieval, secret-key rejection, missing provider URL |
| `Subscriptions/Purchase/Api/PaymentRouteTests.cs` | Injected provider and dynamic policy response |
| `Profiles/Creation/Application/ProfileCreationTests.cs` | Distinct owner age/builder years and out-of-range theory |
| `Profiles/Photo/Application/ProfilePhotoWorkflowTests.cs` | Upload success, failure and CAS mismatch |
| `Profiles/Photo/Contract/CloudinaryHttpAdapterTests.cs` | Signed multipart and provider-failure contract |
| `Publishing/Structure/Api/ProjectStructureRouteTests.cs` | Builder list visibility and legacy structure statuses |

`Modules/TestSupport/CoreBusinessTestSupport.cs` holds the original collaborators
once: isolated InMemory factory, token/HTTP/configuration/signature helpers,
fixed time, recording HTTP handlers and fake upload/payment adapters. It owns
no test cases. Helper visibility is internal so the extracted classes can reuse
them; helper algorithms and responses remain unchanged.

## Mixed Publishing/Subscriptions DDD class replaced

`Modules/PublishingAndSubscriptionsDddTests.cs` becomes five classes/files.
The 10 methods (16 discovered cases) occur exactly once.

| Destination (under `Modules/`) | Original scenarios |
|---|---|
| `Publishing/Units/Application/UnitCommandQueryTests.cs` | Unit create/query; owner projection sync; missing user; invalid unit |
| `Publishing/Clients/Application/ClientCommandQueryTests.cs` | Client CRUD and unit/owner projection linking |
| `Publishing/Structure/Application/ProjectStructureWorkflowTests.cs` | Provisioned project/units/floor devices/unit devices |
| `Subscriptions/Plans/Application/PlanCommandQueryTests.cs` | Plan create/query |
| `Subscriptions/Plans/Unit/PlanResourceTests.cs` | Seven feature-JSON partitions and resource mapping |

These tests reuse the same isolated InMemory context construction from the
helper above. InMemory provisioning counts do not establish SQL atomicity.

## References and compatibility

The six context catalogues are linked from [README](README.md). Evidence-ledger
`owner` paths point to current sources; historical executed commands/results
are retained. Repository workflow/script searches found no filters for either
removed mixed class and no hardcoded moved source paths outside these ledgers.
Class-substring filters such as `IamPersistenceMySqlTests` remain usable;
changed standalone namespaces mean external exact fully-qualified selections
must use the new names from discovery. `AnalyticsInfluxReadTests` and IAM
A/B/C/D retain their original namespaces to preserve execution identity.

Rollback boundary: the listed test sources, `TestKit/Traits.cs`, this README/map,
the reorganization evidence, context catalogues and ledger navigation updates.
No application behavior, project/package configuration or runtime scripts are
part of this work unit.
