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
tests/                           contract tests
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
- [x] **Phase 5** — contract tests comparing old and new responses
- [x] **Phase 6** — switch reminder traffic to the Treatment service
- [x] **Phase 7** — remove reminder code from the monolith

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
| `localhost:8080` | the YARP gateway — the address the frontend uses; reminders now go to the Treatment service, everything else to the monolith |
| `localhost:8081` | the monolith directly, for comparing against the gateway |
| `localhost:8082` | the Treatment service directly, bypassing the gateway |
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

## Switching the traffic

The gateway holds two routes: a catch-all that forwards to the monolith, and a more
specific one for `/api/reminder` that forwards to the Treatment service. Adding the second
route is the whole of the migration as far as the running system is concerned — no service
was redeployed and no line of either service changed.

It landed in two steps. First the route carried an extra condition on an `X-Canary` header,
so it applied only to requests that asked for it; no client sends that header, so nothing
moved while the routing itself was verified on the real path, through the real gateway,
with real cookies and tracing. Only once the full contract had been proven over that path
was the condition removed and the route made unconditional.

The gateway's `appsettings.json` is mounted as a volume rather than baked into the image,
and the gateway runs with a polling file watcher, because bind-mounted files do not deliver
change events into a container on macOS. Together this means flipping the rule takes
effect within seconds, with no restart:

```
switching to the Treatment service   1 s
rolling back to the monolith         5-8 s
```

Rolling back is editing the same file back. That is the property the pattern is chosen for:
the decision is reversible at the cost of a configuration change, not a deployment.

One caveat found the hard way — mounting a single file binds it to an inode, so anything
that replaces the file instead of writing in place, `git checkout` included, silently
detaches the container from it. Recreating the gateway container restores the mount.

## What is left of the monolith

The reminder controller, service, entity, DTOs and the `Reminders` mapping are gone from
the monolith. It no longer knows the feature exists: `/api/reminder` returns 404 there,
while `/api/user`, `/api/entry` and `/api/treatment-process` are unchanged. Requests still
arrive at the same gateway address as before, and the frontend was never touched.

The physical table stayed where it was. Removing the `DbSet` makes EF Core want to drop it
on the next migration — it scaffolds `DropTable("Reminders")` and warns about data loss —
so the generated migration was emptied. It records that the monolith's model no longer
contains reminders without touching the table that the Treatment service now owns.

Two things outlive the code removal and are worth naming rather than hiding. The foreign
key is still there:

```
FK_Reminders_Users_UserId : Reminders -> Users, ON DELETE CASCADE
```

Deleting a user still cascades to their reminders, enforced by a database the monolith no
longer knows it shares. And the Treatment service still reads the `Users` table to check
that a user exists. Both disappear together when the databases are split and Identity
becomes a service of its own — the second one turns into a call over the network, which is
what the `IUserDirectory` port already anticipates.

## Distributed tracing

All three services are instrumented with OpenTelemetry and export over OTLP to Jaeger at
`localhost:16686`. Service names and the exporter endpoint come from environment variables
in the Compose file — the OpenTelemetry SDK reads `OTEL_SERVICE_NAME` and
`OTEL_EXPORTER_OTLP_ENDPOINT` by itself, so neither name appears anywhere in application
code.

Instrumentation went in before the Treatment service existed, on purpose: it captured what
a reminder request looked like while the monolith still served it, so the same request
could be compared after the switch. Before:

```
gateway   GET {**catch-all}        2.66 ms
gateway   GET                      2.43 ms
monolith  GET api/reminder         2.02 ms
monolith  SELECT [u].[Id] ...      1.10 ms
```

After:

```
gateway   GET /api/reminder/{**catch-all}   6.74 ms
gateway   GET                               6.43 ms
treatment GET api/reminder                  5.89 ms
treatment SELECT [Users]                    2.11 ms
treatment SELECT [Reminders]                1.33 ms
```

The client sent the same request and got the same response both times. What changed is
visible only here: a different service in the middle, and two SQL queries where there was
one. The monolith managed with a single query because ReminderService loads reminders
through `Users.Include(u => u.Reminders)` — a join that only works while reminders and
users share a database. The Treatment service cannot keep that shortcut: it asks whether
the user exists through a port that will later become a call to Identity, then reads
reminders on its own. The extra round trip is the visible price of separating the contexts,
not a regression, and it is exactly the kind of consequence tracing was put in place to
expose.

Tracing is not on the critical path. Stopping the Jaeger container leaves every endpoint
working; exports fail silently in the background. That is worth knowing both ways — it
means instrumentation adds no new point of failure, and it means an empty Jaeger UI gives
no clue about why.

## Running the tests

The characterization tests that opened this migration are gone. They pinned down how the
monolith served reminders, and the monolith no longer serves them; their expectations live
on in the contract tests, which assert the same behaviour against the service that took
over. Being able to delete them without losing coverage is what finishing looks like — they
remain in the history, up to the commit that removed them.

### Contract tests

These prove the Treatment service answers exactly like the monolith used to. They talk to
the services over HTTP and reference no project, so they need the stack running and the
secrets exported:

```
docker compose up -d
set -a; . ./.env; set +a
dotnet test tests/IsoTreatment.ContractTests
```

`set -a` marks everything assigned afterwards for export, `. ./.env` runs the file in the
current shell, and `set +a` turns exporting off again. Without it the values would be shell
variables only, invisible to the `dotnet test` child process. Compose does not need this —
it reads `.env` by itself.

The tests seed a dedicated user per test case and delete it afterwards, so they can be run
repeatedly against a database that already holds data. If a service or the database is
unreachable, they fail with a message saying so rather than a wall of timeouts.

Addresses and credentials can be overridden with `CONTRACT_TESTS_MONOLITH_URL`,
`CONTRACT_TESTS_TREATMENT_URL` and `CONTRACT_TESTS_DB_HOST`.

The suite runs against three base addresses: the monolith, the Treatment service and the
gateway. The gateway is the production path, so the same assertions have to hold there
whichever service is behind it — they passed before the switch, when the gateway reached
the monolith, and after it, when it reaches the Treatment service.

One test asserts that a token in the `Authorization` header returns 500 from the monolith
and 200 from the Treatment service. That divergence is deliberate: the monolith
authenticates from the header and then re-reads the cookie, passing null onward. It is
asserted rather than skipped, so that it stays a documented decision instead of an
unnoticed drift.

Fuller technical documentation follows as the implementation progresses.
