# Subscriptions test catalogue

Navigate `backend/tests/Modules/Subscriptions/`. Counts are methods; the plan
feature-deserialization theory expands to seven cases. `U` means no inherited
Risk classification.

| Capability | Relative file under `Modules/Subscriptions/` | Actual layer / dependencies | Inherited tiers |
|---|---|---|---|
| Checkout→confirm→invoices, supersede/cancel/scoping, webhook route/errors/fuzz | `Purchase/Api/SubscriptionPurchaseFlowTests.cs` | API, InMemory, simulated Stripe; happy path retains `Layer=Contract` | A ×11, B, D |
| Injected provider and dynamic methods | `Purchase/Api/PaymentRouteTests.cs` | API, InMemory, fake payment provider | U |
| Activation/supersede and concurrent confirm | `Purchase/Persistence/SubscriptionPersistenceMySqlTests.cs` | Persistence, opt-in MySQL | A ×2 |
| Unsigned callback route | `Webhooks/Api/StripeWebhookRouteTests.cs` | API, InMemory | U |
| Signature, duplicate and legacy metadata | `Webhooks/Application/StripeWebhookWorkflowTests.cs` | Application, InMemory | U ×3 |
| Signed event idempotency | `Webhooks/Application/SubscriptionWebhookIdempotencyTests.cs` | Application, InMemory | A |
| Create/query plans | `Plans/Application/PlanCommandQueryTests.cs` | Application, InMemory repository | U |
| Feature JSON resilience and resource mapping | `Plans/Unit/PlanResourceTests.cs` | Unit, no database/HTTP | U ×2 |
| Restricted-key/dynamic-method options | `StripeAdapter/Unit/StripeOptionsTests.cs` | Unit, no HTTP/database | U |
| Key resolver decisions | `StripeAdapter/Unit/StripeKeyResolverTests.cs` | Unit; inherited `Layer=Application` | A ×3 |
| Outgoing restricted key and receipt mappings | `StripeAdapter/Contract/StripeKeyDisciplineTests.cs` | Adapter contract, fake HTTP; inherited `Api`/`Application` labels | A ×3 |
| Checkout transport, confirmation/invoices and fail-closed configuration | `StripeAdapter/Contract/StripeHttpAdapterTests.cs` | Adapter contract, fake HTTP | U ×4 |

## Run the functionality

```sh
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Context=Subscriptions"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "FullyQualifiedName~PlanResourceTests"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Flow=SUBSCRIPTIONS.PURCHASE&Risk=A"
```

The original `SUBSCRIPTIONS.PURCHASE` Flow is retained even for its webhook
methods. Older CoreBusiness/DDD scenarios retain their Category without new
Flow/Risk claims. Purchase and key-discipline partial classes preserve shared
private helpers and class filters across layer folders; their namespaces stay
the same across all parts.

No fake HTTP or simulated checkout proves a live Stripe account/provider.
InMemory idempotency does not prove SQL uniqueness under race. MySQL methods
require `IOBUILD_TEST_MYSQL_CONNECTION`, otherwise their early return is reported
as a pass by xUnit. The runtime script and fake provider server retain their
original `backend/tests/Integration/` entry-point paths.
[Verification and limitations](../../../backend/tests/reorganization-evidence.md).
