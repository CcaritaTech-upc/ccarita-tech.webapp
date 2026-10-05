# Devices test catalogue

Navigate `backend/tests/Modules/Devices/` for HTTP/persistence and
`backend/tests/Integration/Devices/CommandTelemetry/Application/` for direct
service collaboration. `U` means inherited methods without a Risk trait.

| Capability | File relative to `backend/tests/` | Actual layer / dependencies | Inherited tiers |
|---|---|---|---|
| Technical Story TS04 device list/read ownership by Builder project and Owner unit | `TS/TS04/DeviceListApiTests.cs` | API, InMemory; selectable with `TechnicalStory=TS04` | 4 contract/access cases |
| Owner/Builder device management and anonymous rejection | `Modules/Devices/Management/Api/DeviceManageTests.cs` | API, InMemory | A ×3 |
| Command→telemetry→status, authorization/ranges/power-off | `Modules/Devices/Control/Api/DeviceControlFlowTests.cs` | API, InMemory; MQTT disabled; happy path retains `Layer=Contract` | A ×3 |
| Command serialization, error bodies and command fuzz | `Modules/Devices/Control/Api/DeviceControlRiskTests.cs` | API, InMemory; MQTT disabled | B, C, D |
| Telemetry payload fuzz | `Modules/Devices/Telemetry/Api/TelemetryPayloadRiskTests.cs` | API, InMemory; inherited Flow still `DEVICES.CONTROL` | D |
| Durable command/shadow, ingest/status and device uniqueness | `Modules/Devices/Control/Persistence/DeviceControlMySqlTests.cs` | Persistence, opt-in MySQL; includes `DEVICES.MANAGE` duplicate-device scenario | A ×3 |
| Command acknowledgement/reconciliation, stale/duplicate telemetry, recovery and owner projection repair | `Integration/Devices/CommandTelemetry/Application/IoTWorkflowTests.cs` | Application, InMemory, fake MQTT publisher/Influx sink; three existing classes | U ×11 |

## Run the functionality

```sh
npm --prefix frontend run test:ts -- 04
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Context=Devices"
dotnet test backend/tests/Integration/IoBuild.Integration.Tests.csproj --no-restore --filter "Context=Devices"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Flow=DEVICES.CONTROL&Risk=D"
```

Command and telemetry assertions converge in the same service journey, so the
Integration file remains CommandTelemetry-owned; its assembly name is not
evidence of broker or production database usage. The telemetry partial shares
`DeviceTiersTests` and its original private factory/seed with Control to preserve
the class selection and within-class serialization. Management uniqueness is
catalogued beside Control persistence because the original fixture spans both.

Fake publisher QoS/retain arguments do not prove real MQTT delivery. Recovery
rows in InMemory do not prove durable SQL outbox behavior. The existing
`Integration/run_devices_telemetry_runtime_proof.sh` remains a separate runtime
entry point; it was not executed for this organization-only change. MySQL
methods return early without a dedicated connection.
[Local verification and limits](../../../backend/tests/reorganization-evidence.md).
