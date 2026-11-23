# IoBuild v2

IoBuild v2 migrates the existing microservice system into a modular monolith with one backend, one frontend, and one SQL database. InfluxDB and the Python simulator remain separate integrations.

The migration is delivered incrementally through stacked branches so each foundation and feature slice can be reviewed and integrated independently.

## Architecture

- **One backend** (`iobuild-api`, ASP.NET Core 9) — modular monolith collapsing 6 microservices (IAM, Devices, Projects, Subscriptions, Analytics, Profiles) into a single `IoBuildDbContext` and one MySQL. No YARP gateway, no RabbitMQ, no Redis. Analytics projections (`device_projection`, `project_projection`, `unit_projection`) are kept as plain tables with LWW import; plumbing (consumers/buses) removed.
- **One frontend** (Vue 3 + Vite, served via nginx) — built from `frontend/Dockerfile`, proxied by `nginx` at `/api/` → `iobuild-api:8080`.
- **One MySQL** (`mysql-monolith`) — single database `iobuild`; migrations consolidated under `backend/src/IoBuild.Api/Persistence`.
- **Optional integrations** (profile-gated): `jaeger` (observability, `--profile observability`), `influxdb` + `mosquitto` + `simulator` (telemetry, `--profile telemetry`), all on `iobuild-network` with volumes `mysql_monolith_data`, `influxdb_data`, `mosquitto_data`.

Entry: `nginx` (80) → `iobuild-api` (8080) or frontend SPA. Health at `/health` and `/api/v1/cutover/status`.

## Quickstart

### Local (without Docker)

```bash
export DOTNET_ROOT=/home/arroz/.dotnet
export PATH=$DOTNET_ROOT:$PATH

dotnet restore backend/IoBuild.sln
dotnet build backend/IoBuild.sln --no-restore
dotnet test backend/IoBuild.sln --no-restore
# filtered
dotnet test backend/tests/Architecture --filter Cleanup --no-restore -v n
```

### Docker Compose

```bash
# Validate
docker compose -f docker-compose.yml config
docker compose --profile telemetry config

# Core stack (backend + frontend + mysql + nginx)
docker compose up -d --wait
curl -f http://localhost:80/health
curl -f http://localhost:80/api/v1/cutover/status

# With observability (jaeger at http://localhost:16686)
docker compose --profile observability up -d --wait

# With telemetry (influx + mosquitto + simulator)
docker compose --profile telemetry up -d --wait
docker compose --profile full up -d --wait   # observability + telemetry

# Logs & lifecycle
docker compose logs -f iobuild-api
docker compose ps
docker compose down
docker compose down -v  # clear DB volume
```

See `docs/compose.md` for service table, networks/volumes, healthchecks, and Dockerfile handling.
See `docs/runbook.md` for health, env, and on-call.
See `docs/cutover.md` for freeze→backup→import→verify→switch→stabilize.
See `docs/rollback.md` for restore.

### Frontend

```bash
cd frontend
npm ci
npm run dev    # Vite on http://localhost:5173 (proxies /api to nginx)
npm run build
```

### Docker Builds

```bash
docker build -f backend/Dockerfile -t iobuild-api:local ./backend
docker build -t iobuild-frontend:local ./frontend
```

## Project Layout

```
backend/                 # .NET 9 monolith (IoBuild.Api)
  src/IoBuild.Api/       # Program.cs, modules, persistence
  tests/                 # Architecture, Contract, Modules, Integration
  tools/                 # IoBuild.Cutover, IoBuild.LegacyImporter
frontend/                # Vue 3 SPA
nginx/nginx.conf         # /api → iobuild-api:8080
docker-compose.yml       # final (promoted from docker-compose.cutover.yml)
docker-compose.cutover.yml # cutover reference
docs/                    # runbook, cutover, rollback, compose
.github/workflows/ci.yml # dotnet test + docker build + compose smoke
```

## CI

`ci.yml` runs on `push`/`pull_request` to `main`:

- `setup-dotnet` 9.0.x
- `dotnet restore` / `dotnet build` / `dotnet test backend/IoBuild.sln`
- `docker build` (backend + frontend)
- `docker compose config` validation
- `docker compose up -d --wait` smoke
- `curl -f http://localhost:80/health` and `curl health` checks
- `docker compose down -v` cleanup

## Cutover Note

Retire topology only after stabilization: `docker-compose.yml` is the promoted cutover. Legacy 6-MySQL + gateway + rabbitmq + redis compose is archived in `microservices/`. Roll back via restore — see `docs/rollback.md`.

## Env

| Key | Example |
|---|---|
| `ConnectionStrings__IoBuild` | `Server=mysql-monolith;Port=3306;Database=iobuild;User=root;Password=iobuild` |
| `Jwt:Secret` | `iobuild-development-secret-must-be-replaced-before-production` |
| `Influx:Url/Org/Bucket/Token` | optional — graceful fallback if empty |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://jaeger:4317` |

## Experiment Cards

Diez experiment cards basadas en el repositorio `webapp/`: monolito modular .NET 9 + frontend Vue 3, MySQL 8.0, Docker Compose, GitHub Actions (`.github/workflows/ci.yml`) y el marco **Convergent Testing** (`webapp/convergent-testing/`).

**Fecha de validación:** 2026-11-23. Los experimentos se corrieron en una máquina local (Windows, .NET 9.0.201, Node 22.18, Docker 29.9) y se contrastaron con el historial real de GitHub Actions del repositorio (120 ejecuciones entre 2026-09-05 y 2026-10-05).

### Estado de cada card

| Estado | Significado |
|---|---|
| **Validado** | Se ejecutó el experimento y hay datos medidos (fuente indicada en la card). |
| **Validado (reformulado)** | La idea original no se podía medir tal cual; se ajustó la pregunta o la métrica y esa versión sí se midió. |

| Card | Tema | Estado | Resultado en una línea |
|---|---|---|---|
| E-01 | Gate 1 (Tier A) antes de la suite completa | Validado | La hipótesis **no se cumple**: solo ahorra ~20 % local y ~33 % en CI. |
| E-02 | Convergent Testing: localización de fallos | Validado (reformulado) | 10 fallos reales, todos localizados en una capa concreta del pipeline. |
| E-03 | MySQL 8.0 real vs sin base real | Validado | Sin MySQL, 22 tests pasan en 67 ms sin probar nada; con MySQL real pasan 22/22 en ~14 s (30 de 30 ejecuciones en base limpia). |
| E-04 | Optimización de entrega del frontend | Validado | Reproducido: LCP móvil sigue en 3.91 s (meta 2.5 s no se cumple). |
| E-05 | Tests de arquitectura del monolito | Validado (reformulado) | Detecta 4 de 4 regresiones hacia microservicios; **0 de 1** acoplamientos entre módulos. |
| E-06 | E2E por rol en entorno tipo producción | Validado | 27 escenarios en verde, 0 fallos, 5 omitidos, en 4 de 4 corridas. |
| E-07 | Seguridad en CI | Validado | Hallazgos nuevos: `npm audit` hoy falla el gate; el secreto falso no se bloquea. |
| E-08 | BDD solo en Pull Requests | Validado | Ahorro del 42 % en pushes medido en el CI (264 s → 153.5 s); falta ver el BDD ejecutarse en un PR. |
| E-09 | Evidence ledger y gates | Validado | 25 de 25 riesgos con dueño y fecha. |
| E-10 | Capacidad del login bajo concurrencia | Validado | 0 errores en 1 300 solicitudes, pero p95 = 2.05 s a 50 usuarios (meta 1 s): **no se cumple**. |

> **Cómo leer los números.** Los resultados locales sirven como evidencia local. La disciplina del propio repo (`docs/delivery-discipline.md`) dice que lo que no corre en CI cuenta como `skipped`. Por eso cada card indica si el dato viene de la máquina local o del historial del CI.

---

### E-01 — Gate 1 rápido (Tier A) antes de la suite completa

**Tipo:** técnico (CI) · **Estado:** Validado — la hipótesis **no se cumple** tal como estaba planteada

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Ejecutar primero solo los tests de riesgo crítico (Tier A) reduce el tiempo hasta el primer fallo, sin dejar pasar defectos críticos? |
| **Hipótesis** | Si en cada push o PR corremos `dotnet test --filter "Risk=A"` antes de la suite completa, el tiempo de feedback ante un defecto crítico baja al menos 50 % frente a correr todo junto, sin aumentar más de 10 % el tiempo del job. |
| **Experimento** | **Grupo experimental:** "Gate 1 — Tier A fast" seguido de "Gate 2 — full convergence suite", como está en `ci.yml`.<br>**Control (sin experimentar):** una sola ejecución de `dotnet test backend/IoBuild.sln` sin filtro.<br>Se midió en local (3 corridas por variante, con `--no-build`) y en el historial de GitHub Actions (tiempos por paso y paso donde falló cada ejecución). |
| **Métrica(s)** | (1) Segundos de la ejecución con y sin filtro.<br>(2) Segundos de Gate 1 y Gate 2 en CI.<br>(3) Paso donde fallaron las ejecuciones fallidas y cuánto tardaron en fallar. |
| **Criterio de éxito** | Gate 1 ≤ 50 % del tiempo de la suite completa y el job no crece más de 10 %. |
| **Muestra / Duración** | Local: 3 + 3 corridas. CI: ejecución `37359763317` (push a `main`, 2026-10-05) y revisión de 10 ejecuciones fallidas. |
| **Decisión** | **Resultados:**<br>• Local: Gate 1 = **51.0 s** de media (50.5 / 51.4 / 51.2) y suite completa = **63.8 s** (65.3 / 61.1 / 65.1). Gate 1 es solo **20 % más rápido**. De los 186 tests del proyecto de módulos, 76 son Tier A y tardan 47 s: la mayor parte del costo es fija, no depende de cuántos tests se corran.<br>• CI: Gate 1 = 18 s y Gate 2 = 27 s (Gate 1 es 67 % de Gate 2). Como Gate 1 no reemplaza a Gate 2 sino que se suma, el job `build-and-test` dura 173 s e incluye esos 18 s adicionales (≈ 10 %, justo en el límite).<br>• Historial: de 10 ejecuciones fallidas revisadas, Gate 1 participó en 3 y cortó el job a los 48–54 s. Los fallos del E2E llegaron a los 144–254 s.<br>**Conclusión:** el criterio del 50 % **no se cumple**. Gate 1 no acelera los tests; su valor real es detener el pipeline antes de las builds de Docker y del `compose up` cuando hay un fallo crítico. **Decisión:** mantener Gate 1 con esa justificación, y revisar por qué los tests Tier A tardan casi lo mismo que la suite completa (probablemente un test lento marcado como Tier A). No se inyectaron los 5 defectos planeados; el historial real de fallos lo reemplaza. |

---

### E-02 — Convergent Testing: localizar fallos por capa

**Tipo:** técnico (metodología de testing) · **Estado:** Validado (reformulado) — no se midió tiempo humano de diagnóstico

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Organizar las pruebas por capas y fronteras (Convergent Testing) permite saber en qué capa está un fallo con solo ver en qué paso del pipeline falló? |
| **Hipótesis** | Si cada capa tiene su propio gate (unit, contrato, integración, E2E), cada fallo del pipeline se atribuye a una sola capa, y los fallos de las capas bajas se detectan mucho antes que los del E2E. |
| **Experimento** | **Reformulado:** en lugar de cronometrar personas, se analizó el historial real. Para cada una de las 10 ejecuciones fallidas más recientes del historial, se registró qué job y qué paso falló, y cuánto tardó el job en fallar.<br>**Control (sin experimentar):** una suite agrupada por tecnología donde todo corre junto y el fallo no indica la capa. |
| **Métrica(s)** | (1) Paso donde falla cada ejecución.<br>(2) % de fallos atribuibles a una sola capa.<br>(3) Segundos hasta el fallo por capa. |
| **Criterio de éxito** | ≥ 90 % de fallos atribuidos a una capa y fallos de capas bajas al menos 2× más rápidos que los del E2E. |
| **Muestra / Duración** | 10 ejecuciones fallidas del CI (2026-09-29 a 2026-10-04). Seis de ellas son del 4 de octubre y fallan en el mismo paso, así que pueden compartir una causa. |
| **Decisión** | **Resultados:**<br>• 6 fallaron solo en el E2E de Playwright, 1 solo en Gate 1, 1 solo en Gate 2 y 2 en Gate 1 y E2E a la vez. Es decir, **10 de 10** quedaron ubicados en un paso concreto y 8 de 10 pasaron por una sola capa.<br>• Los fallos de Gate 1 cortaron el job a los **48–54 s**; los del E2E llegaron a los **144–254 s**, es decir, **3 a 5 veces más tarde**.<br>• Casos reales documentados en el repo: un 502 por una regex de nginx sin comillas y un ciclo de inicialización por el chunk de PrimeVue; ambos se diagnosticaron por capa y terminaron en tests de regresión.<br>**Límite:** esto no mide el tiempo que tarda una persona en diagnosticar, así que la parte de "−40 % de tiempo de diagnóstico" sigue sin demostrarse. **Decisión:** adoptar la metodología. Para medir tiempo humano habría que cronometrar a integrantes del equipo. |

---

### E-03 — Probar persistencia con MySQL 8.0 real

**Tipo:** técnico (testing de integración) · **Estado:** Validado — con hallazgos que no estaban en el ledger

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Probar la persistencia contra MySQL 8.0 real detecta algo que no se ve al correr los mismos tests sin una base real? |
| **Hipótesis** | Si los 22 tests de persistencia (`*MySqlTests`) se ejecutan contra MySQL 8.0 real, se prueban de verdad las restricciones de unicidad, transacciones, concurrencia y migraciones, a un costo de tiempo aceptable. |
| **Experimento** | **Control (sin experimentar):** correr los 22 tests sin la variable `IOBUILD_TEST_MYSQL_CONNECTION`.<br>**Grupo experimental:** correrlos contra un contenedor desechable `mysql:8.0` (versión 8.0.46), con el esquema creado por la propia API al arrancar (el historial de migraciones de EF quedó vacío, así que no se aplicaron migraciones de EF).<br>Después se repitió la ejecución varias veces para medir estabilidad. |
| **Métrica(s)** | (1) Tests verdes y tiempo de ejecución.<br>(2) Tests que fallan solo con MySQL real.<br>(3) Estabilidad entre corridas repetidas. |
| **Criterio de éxito** | 22/22 verdes de forma estable con MySQL real, con un costo de tiempo razonable. |
| **Muestra / Duración** | Una ejecución de control y 43 con MySQL real: 13 en la primera tanda (con distintos estados de la base), más 20 del test aislado y 10 del grupo completo en base limpia. |
| **Decisión** | **Resultados:**<br>• **Sin MySQL:** 22/22 "verdes" en **67 ms**. Los tests salen en verde sin probar nada. Como el CI no define la variable, esos 22 tests **tampoco prueban MySQL en el CI**.<br>• **Con MySQL real y el esquema creado por la API (base limpia):** 22 tests en ~14–15 s, y **estable**: el test que había fallado, corrido solo, pasó **20 de 20** veces, y el grupo completo pasó 22/22 en **10 de 10** ejecuciones (0 fallos en 30 ejecuciones).<br>• **Falla no reproducida:** en una base que ya había pasado por corridas fallidas, 2 de 7 ejecuciones fallaron en `Structure_provisions_deterministically_and_rejects_redefine_on_mysql` (error de unicidad en `units`). No se pudo reproducir en base limpia y la causa no está demostrada.<br>• **Con una base vacía:** fallaron 11 de los 22 por falta de esquema; el fixture no crea las tablas. En la corrida siguiente, con el esquema que dejó la propia suite, 2 tests (`Activation_and_supersede_on_mysql` y el de `units`) siguieron fallando de forma repetible por claves únicas. Con el esquema creado por la API ese fallo repetible desapareció.<br>**Conclusión:** la hipótesis se cumple a medias: MySQL real sí prueba lo que InMemory no, pero solo cuando se ejecuta, y hoy eso ocurre únicamente en local. **Decisión:** agregar un servicio MySQL 8.0 al job `build-and-test` con la variable definida y el esquema creado como lo hace la API, y usar una base limpia en cada corrida (el test que falló en una base reutilizada no falló en 30 ejecuciones con base limpia). |

---

### E-04 — Optimización de entrega del frontend

**Tipo:** técnico (rendimiento) · **Estado:** Validado — datos originales del ledger y repetición local

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Habilitar gzip, política de caché estática, separación de chunks, carga diferida de Cloudinary y Stripe y precarga del hero mejora de forma medible el rendimiento percibido en login y registro? |
| **Hipótesis** | El LCP en móvil baja al menos 60 %, el puntaje de Performance de Lighthouse sube al menos 25 puntos y los bytes transferidos bajan al menos 70 %, sin romper ningún flujo. |
| **Experimento** | **Control (sin experimentar):** medición ANTES con Lighthouse 12.8.2 en el mismo origen local, sobre `/iam/login` y `/iam/register-owner`, móvil y escritorio (`docs/performance/evidence-ledger.md`, 2026-09-29).<br>**Grupo experimental:** medición DESPUÉS, y una **repetición hoy** (2026-11-23) sobre `/iam/login` con el stack levantado desde cero. |
| **Métrica(s)** | Performance, LCP (s) y MB transferidos. |
| **Criterio de éxito** | LCP móvil ≤ 2.5 s, Performance ≥ 75 y transferencia ≤ 30 % del control. |
| **Muestra / Duración** | 8 auditorías originales (4 antes + 4 después, una por ruta y perfil) y 2 repeticiones hoy (móvil y escritorio). No son medianas estadísticas. |
| **Decisión** | **Resultados originales (ledger):** login móvil 50 → 81, LCP 15.32 s → 3.91 s, 2.30 MB → 0.48 MB; login escritorio 74 → 99, LCP 2.73 s → 0.87 s; registro Owner móvil 47 → 75, LCP 16.14 s → 4.36 s; registro Owner escritorio 74 → 99, LCP 2.85 s → 0.82 s.<br>**Repetición hoy (login):** móvil **78**, LCP **3.91 s**, 0.47 MB; escritorio **99**, LCP **0.81 s**, 0.48 MB. Los números reproducen los del ledger.<br>**Conclusión:** el criterio se cumple en escritorio y **no** en el LCP móvil (3.91 s, sobre la meta de 2.5 s). **Decisión:** adoptar los cambios; el pendiente es reducir PrimeVue (914 KB de JS inicial) y el CSS. |

---

### E-05 — Tests de arquitectura que protegen la forma del monolito

**Tipo:** técnico (arquitectura) · **Estado:** Validado (reformulado) — la comparación contra microservicios no aplica

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Los tests de arquitectura del monolito detectan que alguien reintroduzca piezas de microservicios (gateway, RabbitMQ, Redis, varias bases) o que un módulo invada a otro? |
| **Hipótesis** | Si se introduce una regresión de ese tipo en una rama, los tests de `backend/tests/Architecture` fallan y bloquean el cambio; en 100 % de los casos. |
| **Experimento** | **Control (sin experimentar):** sin esos tests, cualquiera de estos cambios compila y no avisa.<br>**Grupo experimental:** en una copia descartable del repo se inyectaron 5 violaciones, una por una, y se corrió el proyecto de tests de arquitectura (17 tests):<br>1. Un servicio `rabbitmq` en `docker-compose.yml`.<br>2. Un segundo servicio MySQL (`mysql-iam`).<br>3. El paquete `RabbitMQ.Client` en el `csproj`.<br>4. Una referencia a `StackExchange.Redis` en el código fuente.<br>5. Una clase de `Subscriptions` que depende de un servicio de `IAM` (acoplamiento entre módulos). |
| **Métrica(s)** | Violaciones inyectadas vs violaciones detectadas. |
| **Criterio de éxito** | 5 de 5 detectadas. |
| **Muestra / Duración** | 1 corrida base + 5 inyecciones, una sesión. |
| **Decisión** | **Resultados:** base 17/17 en verde. Las inyecciones 1, 2, 3 y 4 fueron **detectadas** (4 de 4; la 3 disparó 2 tests). La inyección 5, el acoplamiento entre módulos, **no fue detectada** (los 17 tests siguieron en verde).<br>**Conclusión:** el monolito está protegido contra volver a microservicios, pero **no** contra que sus módulos se acoplen entre sí, porque `ModuleBoundaryTests` solo verifica que exista un único `DbContext`. **Decisión:** agregar una regla de dependencias entre namespaces de módulos (por ejemplo con NetArchTest). *Nota:* esta card ya no compara con una topología anterior, porque en este repositorio no hay evidencia de esa topología más que el texto del README. |

---

### E-06 — E2E por actor (Builder y Owner) en entorno tipo producción

**Tipo:** técnico (E2E en CI) · **Estado:** Validado

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Exigir evidencia de camino feliz para cada rol (Builder y Owner) evita que queden roles sin probar en journeys transversales? |
| **Hipótesis** | Si cada journey transversal debe tener su propio E2E por rol, el % de roles con evidencia pasa de 50 % (solo Owner) a 100 %, y la suite completa corre estable contra el sistema real. |
| **Experimento** | **Control (sin experimentar):** cubrir login y registro solo para `Owner`; en la práctica `Builder` quedó sin probar (`docs/delivery-discipline.md` §7).<br>**Grupo experimental:** el job `frontend-e2e`. Se reprodujo en local: `docker compose up` desde cero (nginx + frontend + API + MySQL 8.0, sin leer `.env`) y Playwright con `CI=true`, igual que en el CI. |
| **Métrica(s)** | (1) Escenarios verdes y fallidos.<br>(2) Roles con camino feliz propio.<br>(3) Tiempo de ejecución. |
| **Criterio de éxito** | 100 % de los roles con evidencia propia, 0 fallos y 0 reintentos para pasar. |
| **Muestra / Duración** | 4 ejecuciones locales completas (32 escenarios cada una) y el historial del CI (paso E2E: 46–55 s en ejecuciones exitosas). |
| **Decisión** | **Resultados:** **27 escenarios en verde, 0 fallidos, 5 omitidos**, en 49 s la primera vez; en 3 repeticiones seguidas dio 27/27 en 40.3, 41.2 y 38.7 s: **4 de 4 corridas en verde, 0 fallos y 0 reintentos**. Los 5 omitidos requieren banderas explícitas de entorno (`E2E_CLOUDINARY_DUMMY`, `E2E_NGINX`, etc.) que el propio ledger documenta. Hay caminos felices separados para Builder (`iam-builder.spec.js`) y para Owner (`iam-happy-path.spec.js`), y pruebas de perfiles y de rendimiento por rol. El stack levantó en 23 s con los contenedores sanos, sin contar la construcción de las imágenes. También pasaron 79 tests unitarios del frontend (12 archivos).<br>**Dato del historial:** 8 de las 10 ejecuciones fallidas revisadas fallaron en este E2E, así que es el paso que más defectos reales detecta.<br>**Decisión:** adoptar la regla de cobertura por rol y extenderla a Devices y Analytics. |

---

### E-07 — Seguridad "shift-left": TruffleHog, auditoría de dependencias y CodeQL

**Tipo:** técnico (seguridad en CI) · **Estado:** Validado — con dos hallazgos relevantes

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Automatizar el escaneo de secretos, la auditoría de dependencias y el análisis estático en el pipeline detecta riesgos antes de que lleguen a `main`? |
| **Hipótesis** | Con los jobs `secret-scan`, `dependency-audit` y `codeql`, los secretos verificados y las vulnerabilidades High/Critical que llegan a `main` son 0, y un secreto sembrado se bloquea el 100 % de las veces. |
| **Experimento** | **Control (sin experimentar):** revisión manual ocasional.<br>**Grupo experimental:** se reprodujeron los controles en local: `npm audit --audit-level=critical`, `dotnet list package --vulnerable --include-transitive` y TruffleHog (misma imagen fijada por digest que usa el CI) sobre el historial real. Además se sembró un secreto **falso** con forma de token de GitHub en un repositorio descartable y se escaneó con la configuración del CI y con una más amplia. |
| **Métrica(s)** | (1) Secretos verificados y no verificados.<br>(2) Vulnerabilidades por severidad.<br>(3) Secretos sembrados bloqueados.<br>(4) Tiempo de los jobs en CI. |
| **Criterio de éxito** | 0 secretos verificados, 0 High/Critical sin dueño y 100 % de lo sembrado bloqueado. |
| **Muestra / Duración** | 1 escaneo del historial real (2 420 fragmentos, 3.1 MB, 27 s), 1 repositorio sembrado escaneado de 2 formas y las auditorías de dependencias de hoy. En CI: secret-scan 10 s, dependency-audit 25–29 s y CodeQL 175–180 s. |
| **Decisión** | **Resultados:**<br>• **Secretos:** el historial real tiene **0 verificados y 0 no verificados**. ✔<br>• **Secreto sembrado:** con la configuración del CI (`--results=verified --fail`) **no se bloquea** (exit 0), porque solo falla con secretos *verificados* como activos, y uno falso no lo es. Al incluir no verificados sí se detecta (exit 183). ✘ El 100 % de lo sembrado **no** se cumple.<br>• **Dependencias frontend:** `npm audit` reporta hoy **27 vulnerabilidades (1 crítica, 18 altas, 8 moderadas)** y sale con código 1, de modo que **el gate `--audit-level=critical` fallaría hoy** aunque el CI del 2026-10-05 estaba en verde: aparecieron advisories nuevos desde entonces. La crítica es una dependencia transitiva y en las que revisé hay corrección disponible.<br>• **Dependencias backend:** `dotnet list package --vulnerable` no listó paquetes vulnerables (la salida solo mostró los orígenes de NuGet).<br>**Decisión:** mantener los tres jobs, pero (1) actualizar las dependencias del frontend antes de que el CI se ponga rojo, y (2) decidir si el escaneo de secretos debe incluir los no verificados, sabiendo que habrá más falsos positivos. CodeQL solo corre en GitHub y no se reprodujo en local. |

---

### E-08 — BDD con Cucumber solo en Pull Requests

**Tipo:** técnico (pipeline) y de producto (historias de usuario) · **Estado:** Validado — el ahorro en pushes está medido en el CI; la ejecución en un PR queda pendiente

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Correr las historias de usuario ejecutables (US01–US10) solo en PRs mantiene la trazabilidad con las historias sin encarecer cada push? |
| **Hipótesis** | Si `npm run test:bdd` se ejecuta solo en `pull_request`, los pushes se aceleran al menos 20 % y el 100 % de las historias sigue verificado antes de integrar. |
| **Experimento** | **Grupo experimental:** el paso condicional de `ci.yml` (`if: github.event_name == 'pull_request'`, commit `3c7bcfd` del 2026-10-05 13:48).<br>**Control (sin experimentar):** el mismo paso sin condición, que corrió en cada push entre el commit `2bb119c` (2026-10-05 00:31) y `3c7bcfd`. Se compararon las duraciones reales del job `frontend-e2e` en ambos periodos y se corrió el BDD completo en local contra el stack levantado. |
| **Métrica(s)** | (1) Escenarios y pasos verdes.<br>(2) Historias cubiertas.<br>(3) Minutos que añade el BDD al job. |
| **Criterio de éxito** | Pushes ≥ 20 % más rápidos, 10 de 10 historias verificadas en PR. |
| **Muestra / Duración** | CI: 4 pushes con el BDD sin condición y 2 pushes con la condición (todos del 2026-10-05). Local: 1 ejecución completa del BDD. |
| **Decisión** | **Resultados:**<br>• **CI, control (BDD en cada push):** el job `frontend-e2e` duró 247, 250, 275 y 285 s (media **264 s**); el paso BDD tardó 107, 110, 124 y 134 s (media **119 s**) y pasó en las 4 ejecuciones.<br>• **CI, con la condición:** el job duró 150 y 157 s (media **153.5 s**) y el paso BDD salió como `skipped`.<br>• **Ahorro medido en push: 42 %** (264 s → 153.5 s), por encima del 20 % de la meta. ✔<br>• **Local:** **32 escenarios y 139 pasos en verde**, cubriendo las **10 historias (US01–US10)**, en 146 s.<br>**Pendiente:** desde que se agregó la condición no ha habido ningún PR, así que **no hay evidencia en el CI de que el paso BDD se ejecute en un PR** (el último PR del historial es del 2026-10-04, anterior al paso). Esa parte del criterio (10 de 10 historias verificadas en PR) se confirma al abrir un PR.<br>**Decisión:** mantener la condición; confirmarla con un PR a `main`. |

---

### E-09 — Evidence ledger y quality gates G0–G4

**Tipo:** de proceso (calidad de entrega) · **Estado:** Validado

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Exigir un *evidence ledger* por cambio, con riesgos que tengan dueño y fecha, reduce los cambios que se declaran "terminados" sin evidencia? |
| **Hipótesis** | Con la disciplina de `docs/delivery-discipline.md`, el 100 % de los riesgos abiertos y diferimientos tiene dueño y fecha de revisión. |
| **Experimento** | **Grupo experimental:** las reglas de la disciplina aplicadas a los 6 contextos (IAM, Profiles, Publishing, Subscriptions, Devices, Analytics) y al reporte transversal de rendimiento.<br>**Control (sin experimentar):** reporte informal de "tests pasan" sin ledger.<br>Se escribió un script que lee los 7 ledgers y cuenta los riesgos abiertos, y cuántos tienen dueño y fecha de revisión. |
| **Métrica(s)** | (1) % de riesgos con dueño.<br>(2) % de riesgos con fecha de revisión.<br>(3) Contextos con ledger. |
| **Criterio de éxito** | 100 % de riesgos con dueño y con fecha; 6 de 6 contextos con ledger. |
| **Muestra / Duración** | 7 archivos de ledger (6 contextos + rendimiento), 25 riesgos o diferimientos abiertos. |
| **Decisión** | **Resultados:** **25 de 25** riesgos o diferimientos tienen dueño y **25 de 25** tienen fecha. Hay **6 de 6** contextos con ledger. Los de Analytics y Devices no tienen riesgos abiertos registrados (los demás sí: IAM 6, Profiles 3, Publishing 2, Subscriptions 1 y rendimiento 13).<br>**Límite:** el conteo mide que el dato exista, no que la fecha se haya cumplido; varias fechas de revisión (por ejemplo 2026-10-06 y 2026-10-07) ya pasaron.<br>**Decisión:** mantener la disciplina. Pendiente: pasar a `passed` los gates marcados `skipped — CI not run` cuando el CI los ejecute, y revisar las fechas vencidas. |

---

### E-10 — Capacidad del login bajo concurrencia (prueba de carga)

**Tipo:** técnico (rendimiento y carga) · **Estado:** Validado — la hipótesis **no se cumple** a 50 usuarios concurrentes

| Campo | Contenido |
|---|---|
| **Pregunta (Q)** | ¿Cuántos inicios de sesión simultáneos soporta el monolito, en un solo host Docker, manteniendo una latencia p95 ≤ 1 s y 0 errores? |
| **Hipótesis** | Hasta 50 usuarios concurrentes iniciando sesión, el p95 se mantiene ≤ 1 000 ms y no hay errores. |
| **Experimento** | **Control (línea base, sin carga):** 1 usuario secuencial, 100 solicitudes.<br>**Grupo experimental:** 10, 50 y 100 usuarios concurrentes (200, 400 y 600 solicitudes) con `POST /api/v1/sessions` contra el stack completo (nginx → API → MySQL 8.0 en Docker), con una cuenta Builder real creada por la API. Script en Node con `fetch`, ejecutado en la misma máquina. |
| **Métrica(s)** | Latencia p50, p95, p99 y máxima (ms), solicitudes por segundo (rps) y tasa de errores. |
| **Criterio de éxito** | p95 ≤ 1 000 ms y 0 errores a 50 usuarios concurrentes. |
| **Muestra / Duración** | 4 niveles de concurrencia, 1 300 solicitudes en total, una corrida por nivel (no se repitió, así que no es una mediana). |
| **Decisión** | **Resultados** (tabla detallada debajo): con 1 usuario el p95 es 182 ms; con 10 usuarios, 301 ms; con 50, **2 051 ms**; con 100, **6 634 ms**. Hubo 0 errores en las 1 300 solicitudes.<br>**Conclusión:** el servicio **no se cae**, pero el criterio **no se cumple a 50 usuarios** (p95 = 2.05 s). Se cumple hasta 10 usuarios concurrentes. La capacidad se estanca en ~40 rps desde los 10 usuarios y, a partir de ahí, la latencia crece con la concurrencia. Un solo inicio de sesión ya tarda ~153 ms; esto es consistente con un hash de contraseña costoso (el backend usa BCrypt), pero **no se hizo un perfilado**, así que la causa no está demostrada.<br>**Límites:** un solo host, sin red real, una corrida por nivel, y solo se midió el login (que hace hash), no los endpoints de lectura. Los datos de la prueba se eliminaron junto con el stack.<br>**Decisión:** no prometer 50 logins simultáneos dentro de 1 s con la configuración actual. Antes habría que perfilar el login y repetir la medición 3 veces para sacar medianas. |

**Resultados detallados de E-10** (`POST /api/v1/sessions`, una corrida por nivel):

| Concurrencia | Solicitudes | Errores | rps | p50 | p95 | p99 | Máx |
|---|---|---|---|---|---|---|---|
| 1 (control) | 100 | 0 | 6.4 | 153 ms | 182 ms | 215 ms | 215 ms |
| 10 | 200 | 0 | 37.3 | 263 ms | 301 ms | 320 ms | 327 ms |
| 50 | 400 | 0 | 40.1 | 1 187 ms | 2 051 ms | 2 261 ms | 2 437 ms |
| 100 | 600 | 0 | 42.4 | 1 515 ms | 6 634 ms | 10 556 ms | 12 770 ms |

---

### Hallazgos que conviene tener presentes

1. **Los tests de MySQL no corren contra MySQL en el CI** (E-03): pasan en 67 ms sin base real.
2. **El gate `npm audit` fallaría hoy** (E-07): 1 crítica y 18 altas.
3. **El acoplamiento entre módulos no tiene ninguna protección** (E-05).
4. **Desde que el BDD se limitó a PRs no ha habido ningún PR**, así que no hay evidencia en el CI de que el paso corra en un PR (E-08). Lo que sí está medido: la condición ahorra 42 % del job en cada push.
5. **Gate 1 no acelera los tests tanto como se esperaba** (E-01): ahorra ~20 % local y ~33 % en CI.
6. **El secreto falso no se bloquea con la configuración actual de TruffleHog** (E-07), porque solo falla con secretos verificados.
7. **El login se estanca en ~40 solicitudes por segundo** (E-10): a 50 usuarios simultáneos el p95 es 2.05 s.

### Qué no se validó

- **CodeQL** (E-07): solo corre en GitHub; se usó el tiempo del historial.
- **Tiempo humano de diagnóstico** (E-02): requiere cronometrar a personas.
- **Los 5 defectos Tier A inyectados** (E-01): se sustituyeron por el historial real de fallos.
- **Causa del fallo único de MySQL** (E-03): no se reprodujo en base limpia.
- **Causa de la saturación del login** (E-10): sin perfilado; solo se midió una corrida por nivel.
- Se descartó la card anterior de A/B de planes de suscripción porque requería usuarios reales; E-10 la reemplaza.

### Cómo se hizo (reproducibilidad)

- Todo se ejecutó sin leer `.env` ni `appsettings`; los contenedores y bases son desechables y ya se eliminaron.
- El stack de Docker se levantó como proyecto aislado (`-p e06cards`) y sin los `container_name` fijos, para no tocar los contenedores detenidos que ya existían en la máquina.
- Las inyecciones de E-05 y el secreto falso de E-07 se hicieron en copias descartables, fuera del repositorio.
- No se editó ningún archivo de `webapp/`. Los cambios sin commit que muestra `git status` en esa carpeta ya existían antes de estas pruebas; las compilaciones solo regeneran `bin/` y `obj/`.
