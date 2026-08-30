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
- the Treatment service, laid out as Domain, Application, Infrastructure and Api, and
  sharing the monolith's database as a bridge,
- **YARP** as a reverse proxy at the system boundary — the single place where the decision
  "who serves this request" is made,
- **Jaeger** for distributed tracing, so that switching traffic is observable rather than
  asserted,
- **Docker Compose** tying the pieces together.

## Repository layout

```
IsoTreatmentProcessSupportAPI/   the monolith
ApiGateway/                      the YARP reverse proxy
TreatmentService/                the extracted service — Domain, Application,
                                 Infrastructure, Api
tests/                           characterization tests capturing current behaviour
docker-compose.yml               gateway, monolith, Treatment, SQL Server, Jaeger
IsoTreatment.http                requests against the monolith through the gateway
TreatmentService.http            requests against the Treatment service, and the
                                 side-by-side pairs that compare it to the monolith
```

## Progress

- [x] **Phase 0** — characterization tests around the reminder API
- [x] **Phase 1** — containerize the monolith as it is
- [x] **Phase 2** — put YARP in front, with all traffic still reaching the monolith
- [x] **Phase 3** — OpenTelemetry instrumentation exported to Jaeger
- [x] **Phase 4** — the Treatment service
- [ ] **Phase 5** — contract tests comparing old and new responses
- [ ] **Phase 6** — switch reminder traffic to the Treatment service
- [ ] **Phase 7** — remove reminder code from the monolith

## Running the application

Docker is the only prerequisite — the monolith and SQL Server both run in containers.

Copy `.env.example` to `.env` and fill it in — it documents every variable Compose
expects and why. Only the SMTP entries are optional; without them registration and
password reset return 500, and nothing else is affected.

```
cp .env.example .env
docker compose up -d --build
```

Compose waits for SQL Server to report healthy before it starts the monolith, so the first
run takes about a minute. On Apple Silicon the database runs under emulation; the Compose
file pins it to `linux/amd64` because SQL Server has no arm64 image.

Four services come up:

| Address | What |
| --- | --- |
| `localhost:8080` | the YARP gateway — the address the frontend uses |
| `localhost:8081` | the monolith directly, for comparing against the gateway |
| `localhost:8082` | the Treatment service directly — no gateway traffic reaches it yet |
| `localhost:16686` | Jaeger UI |
| `localhost:14330` | SQL Server |

Note that `--build` rebuilds images, while a plain `docker compose up -d` only recreates
containers. Changing a value in `.env` needs the latter; changing code or packages needs
the former.

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

### Getting a token

Every reminder endpoint requires a JWT, which the API reads from an HttpOnly cookie named
`token`. Registering a user through the API will not get you one: registration leaves the
account unconfirmed and login rejects it until the confirmation mail is answered.

For local work, mint a token directly for a user id that exists in `Users`:

```
set -a; . ./.env; set +a
b64u() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }
H=$(printf '%s' '{"alg":"HS256","typ":"JWT"}' | b64u)
P=$(printf '{"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier":"1","exp":%s,"iss":"isotreatment-users-issuer","aud":"isotreatment-users-audience"}' $(( $(date +%s) + 3600 )) | b64u)
S=$(printf '%s' "$H.$P" | openssl dgst -sha256 -hmac "$AUTHENTICATION_SIGNING_KEY" -binary | b64u)
echo "$H.$P.$S"
```

Paste the result into the `@token` variable at the top of either `.http` file. It lasts an
hour; a sudden run of 401s usually means it expired.

The claim type is the full URI, not the short `nameid`. The monolith builds its tokens with
an explicit claim list, which skips the short-name mapping, so a token using `nameid` would
pass signature validation and then fail to yield a user id.

### Checking that it works

| Request | Expected |
| --- | --- |
| `GET localhost:8080/swagger/index.html` | 200 — the application started |
| `GET localhost:8080/api/reminder` | 401 — routing and authentication are wired |
| `POST localhost:8080/api/user/login` with unknown credentials | 400 — the application reached the database |

A 500 on the last one means the database is unreachable or the schema was never applied.
That distinction is worth remembering: both cases look identical from the outside.

## Distributed tracing

The gateway and the monolith are instrumented with OpenTelemetry and export over OTLP to
Jaeger at `localhost:16686`. Service names and the exporter endpoint come from environment
variables in the Compose file — the OpenTelemetry SDK reads `OTEL_SERVICE_NAME` and
`OTEL_EXPORTER_OTLP_ENDPOINT` by itself, so neither name appears anywhere in application
code.

Send a request through the gateway and one trace should span both services and the SQL
query underneath:

```
gateway   GET {**catch-all}          117.38 ms
gateway   GET                        116.50 ms
monolith  GET api/reminder           115.47 ms
monolith  SELECT [u].[Id] ...          6.29 ms
```

That single SQL span is the baseline for Phase 6. Reading reminders costs exactly one
query today, because ReminderService loads them through `Users.Include(u => u.Reminders)` —
a join that only works while reminders and users share a database. The Treatment service
cannot keep that shortcut: it asks whether the user exists through a port that will later
become a call to Identity, and then reads reminders on its own. The same trace will show
two queries instead of one. That is the visible price of separating the contexts, not a
regression, and it is exactly the kind of consequence tracing was put in place to expose.

Tracing is not on the critical path. Stopping the Jaeger container leaves every endpoint
working; exports fail silently in the background. That is worth knowing both ways — it
means instrumentation adds no new point of failure, and it means an empty Jaeger UI gives
no clue about why.

## Running the tests

The characterization tests start their own throwaway SQL Server container, independent of
the Compose stack, so Docker has to be running:

```
dotnet test tests/IsoTreatmentProcessSupportAPI.CharacterizationTests
```

Fuller technical documentation follows as the implementation progresses.
