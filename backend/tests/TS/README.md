# Technical Story API Tests

Run one story from the repository's `frontend` directory:

```sh
npm run test:ts -- 01
```

Accepted inputs are `01`–`05` or `TS01`–`TS05`. Tests run in the existing `IoBuild.Modules.Tests.csproj`; each test class is tagged with its `TechnicalStory` ID. The Gherkin User Story suite remains under `test/Features/`.

## Story-to-test traceability

| Story | Epic | Priority | Aligned API contract | Test file | Command |
|---|---|---|---|---|---|
| TS01 — List projects for the authenticated builder | EP08 | High | `GET /api/v1/projects` derives builder identity from the JWT. Returns `200` with that builder's project resources, or `200 []` when there are none. There is no `builderId` query parameter. | `TS01/ProjectListApiTests.cs` | `npm run test:ts -- 01` |
| TS02 — Create a project | EP08 | High | `POST /api/v1/projects` accepts `name`, `description`, `location`, `totalUnits`, and optional `builderId`/`imageUrl`. Returns `201` with the created project; `createdAt` is server-generated. Invalid project text returns `422 { error }`; a mismatched `builderId` returns `403`. | `TS02/ProjectCreateApiTests.cs` | `npm run test:ts -- 02` |
| TS03 — List builder clients | EP07 | High | `GET /api/v1/clients` is Builder-only and supports optional `builderId` (must match the JWT) and `projectId`. Returns `200` with client resources or `200 []`. No server-side name/status filter, pagination, sorting, or pagination metadata is currently implemented. | `TS03/ClientListApiTests.cs` | `npm run test:ts -- 03` |
| TS04 — List devices by project or unit | EP09 | High | `GET /api/v1/devices` requires authentication, scopes Builders to their projects and Owners to assigned units, and supports optional `projectId` and `unitId`. Resources expose `id`, `name`, `type`, `location`, `macAddress`, `projectId`, `status`, and `unitId`; live status is a separate `/devices/{id}/status` endpoint. No location/status list filter is implemented. | `TS04/DeviceListApiTests.cs` | `npm run test:ts -- 04` |
| TS05 — Register a user | EP02 | High | `POST /api/v1/users` returns `201 { message }` for valid registration; it does not return the user or an access token. Duplicate email returns `409 { error }`; invalid registration data returns `400 { error }`. The persisted password is hashed. | `TS05/UserRegistrationApiTests.cs` | `npm run test:ts -- 05` |

## Contract alignment notes

- Project listing is scoped by the authenticated Builder, not a caller-supplied `builderId` query value. The response contains `occupiedUnits`, `imageUrl`, `structureDefined`, and `createdAt`; it does not expose a project `status` or `occupancyRate` field.
- Client listing is scoped by the authenticated Builder. The resource uses `fullName`, `projectName`, and `accountStatement`, and includes `deviceCount`; it does not use `associatedProject` or return pagination metadata.
- Device list, detail, status, and energy reads are scoped to Builder-owned projects or Owner-assigned units. Tests verify cross-owner reads return `404`; only authenticated Builder and Owner roles receive a list.
- Builder's device-management view still uses `GET /api/v1/devices` without query filters and now receives all devices in that Builder's projects. Owner's unit-device table continues to come from the owner Analytics payload; its per-device status reads now enforce unit assignment.
- The original TS03 name/status/pagination criteria and TS04 location/status filter plus `realTimeStatus` criteria do not match the current API contract; this suite documents the implemented behavior rather than silently claiming those features exist.
