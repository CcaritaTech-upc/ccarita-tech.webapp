# Executable User Story scenarios

The `.feature` files in this folder are the canonical Gherkin scenarios for the
User Stories. Cucumber.js executes them with Playwright; step definitions and
browser setup live in `frontend/tests/bdd/` so they run with the existing
frontend test toolchain.

## Current executable coverage

All ten User Stories have Cucumber step definitions and Playwright-backed
scenarios. The app-facing route/API details in the scenarios follow the current
implementation (for example, login is `/iam/login`).

| User Story | Feature | Automated status |
|---|---|---|
| US01 | `US01VisualizarDispositivos.feature` | `frontend/tests/bdd/steps/us01-devices.steps.js` |
| US02 | `US02AccederPerfilUsuario.feature` | `frontend/tests/bdd/steps/us02-profile-access.steps.js` |
| US03 | `US03EditarInformacionPerfil.feature` | `frontend/tests/bdd/steps/us03-profile.steps.js` |
| US04 | `US04VerListaProyectos.feature` | `frontend/tests/bdd/steps/us04-project-list.steps.js` |
| US05 | `US05AgregarNuevoProyecto.feature` | `frontend/tests/bdd/steps/us05-project-create.steps.js` |
| US06 | `US06VerDetallesProyecto.feature` | `frontend/tests/bdd/steps/us06-project-details.steps.js` |
| US07–US08 | `US07VerListaClientes.feature` – `US08AgregarNuevoCliente.feature` | `frontend/tests/bdd/steps/us07-us08-clients.steps.js` |
| US09 | `US09VerPlanSuscripcion.feature` | `frontend/tests/bdd/steps/us09-subscription.steps.js` |
| US10 | `US10IniciarSesion.feature` | `frontend/tests/bdd/steps/us10-login.steps.js` |

Run one User Story from the repository root with a single command. The script
starts a disposable local Compose stack, runs that story, and removes its test
database afterward:

```sh
npm --prefix frontend run test:us -- 01
```

Use `01` through `10` (or `US01` through `US10`); for example,
`npm --prefix frontend run test:us -- US03`. From inside `frontend/`, the
equivalent is `npm run test:us -- US03`.

Latest local full-suite run: 32 scenarios / 139 steps passed against an
isolated Compose stack. CI is configured to run the same suite, but has not yet
run for these local changes. Tests create disposable accounts and data;
Owner registration provisions an assigned unit. Example emails in the Gherkin
are aliases, not live credentials.

US01 reserves projects with IDs 1–3 for the demo devices seeded by the app, so
the generated test projects do not accidentally inherit those devices.

`@cucumber/cucumber` is a frontend development dependency. The runner and step
definitions are used only by tests and are not imported into the production app.
Never set `E2E_BASE_URL` to the production host; the scenarios create test users
and other test records.
