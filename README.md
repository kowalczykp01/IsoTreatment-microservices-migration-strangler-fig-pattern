# IsoTreatment — Monolith to Microservices Migration

This repository accompanies a master's thesis on migrating an existing ASP.NET Core
monolith to microservices. It is not a greenfield project: the starting point is a working
application, and every step is meant to be reproducible and verifiable rather than merely
described.

## The application being migrated

`IsoTreatmentProcessSupportAPI` is an ASP.NET Core Web API (.NET 8) supporting patients
who follow a long-term medication regimen. It manages user accounts along with their
pharmacological parameters, diary entries, medication reminders, and treatment-process
calculations. Today it runs as a single process backed by a single SQL Server database,
with controllers organised around entities rather than around business capabilities.

Its full commit history is preserved here. The application also continues to live in its
own repository at https://github.com/kowalczykp01/IsoTreatmentProcessSupportAPI, wired
here as the read-only `upstream` remote.

## Target architecture

The monolith is to be decomposed into two business services:

- **Identity** — authentication, identity, and token issuing.
- **Treatment** — the treatment domain: treatment profiles, diary entries, and reminders.

## Current scope

This repository demonstrates the **Strangler Fig Pattern** on one complete slice of
functionality: extracting **reminders** from the monolith into the **Treatment** service.

The service is named Treatment from its first commit even though reminders are, for now,
the only thing it serves. Later waves move diary entries and treatment profiles into the
same service, and renaming a running service is exactly the cost this naming avoids.

The moving parts:

- the monolith, whose reminder code stays untouched until the final cleanup step,
- the Treatment service, sharing the monolith's database as a bridge,
- **YARP** as a reverse proxy at the system boundary — the single place where the decision
  "who serves this request" is made,
- **Jaeger** for distributed tracing, so that switching traffic is observable rather than
  asserted,
- **Docker Compose** tying the pieces together.

## Repository layout

```
IsoTreatmentProcessSupportAPI/   the monolith, with its Dockerfile
tests/                           characterization tests capturing current behaviour
docker-compose.yml               the monolith and SQL Server
```

## Progress

- [x] **Phase 0** — characterization tests around the reminder API
- [x] **Phase 1** — containerize the monolith as it is
- [x] **Phase 2** — put YARP in front, with all traffic still reaching the monolith
- [ ] **Phase 3** — OpenTelemetry instrumentation exported to Jaeger
- [ ] **Phase 4** — the Treatment service
- [ ] **Phase 5** — contract tests comparing old and new responses
- [ ] **Phase 6** — switch reminder traffic to the Treatment service
- [ ] **Phase 7** — remove reminder code from the monolith

## Running the application

Docker is the only prerequisite — the monolith and SQL Server both run in containers.

Create a `.env` file in the repository root with the two secrets Compose expects:

```
MSSQL_SA_PASSWORD=<password meeting SQL Server complexity rules>
AUTHENTICATION_SIGNING_KEY=<HMAC key used to sign and validate JWTs>
```

Then bring the stack up:

```
docker compose up -d --build
```

Compose waits for SQL Server to report healthy before it starts the monolith, so the first
run takes about a minute. On Apple Silicon the database runs under emulation; the Compose
file pins it to `linux/amd64` because SQL Server has no arm64 image.

### Applying the database schema

**The schema is not created automatically.** The monolith does not run migrations at
startup, deliberately: keeping migration logic out of its code means the only change the
migration required of the monolith was moving the connection string into configuration.

Apply the migrations from the host, against the port Compose publishes:

```
set -a; . ./.env; set +a
ConnectionStrings__IsoSupportDb="Server=localhost,14330;Database=IsoTreatmentProcessSupport;User Id=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=true;TrustServerCertificate=true;" \
  dotnet ef database update --project IsoTreatmentProcessSupportAPI
```

This needs the EF Core tools (`dotnet tool install --global dotnet-ef`). It is a one-off
step, but it has to be repeated whenever the `mssql-data` volume is removed, because the
database disappears with it.

### Checking that it works

| Request | Expected |
| --- | --- |
| `GET localhost:8080/swagger/index.html` | 200 — the application started |
| `GET localhost:8080/api/reminder` | 401 — routing and authentication are wired |
| `POST localhost:8080/api/user/login` with unknown credentials | 400 — the application reached the database |

A 500 on the last one means the database is unreachable or the schema was never applied.
That distinction is worth remembering: both cases look identical from the outside.

## Running the tests

The characterization tests start their own throwaway SQL Server container, independent of
the Compose stack, so Docker has to be running:

```
dotnet test tests/IsoTreatmentProcessSupportAPI.CharacterizationTests
```

Fuller technical documentation follows as the implementation progresses.
