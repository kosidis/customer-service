---
name: helm
description: WHEN changing a Helm chart (apps/api/helm/customer-service or apps/web/helm/customer-service-web), adding a value or environment, or deploying and debugging the services on Kubernetes.
---

# Helm charts

Two charts, one per app, both `apiVersion: v2`, `version` 0.1.0, `appVersion` 1.0.0, built and tested with Helm 4:

- `apps/api/helm/customer-service`: the API (most of this skill).
- `apps/web/helm/customer-service-web`: the web app (see "Web chart" below).

## Layout

- `values.yaml`: shared defaults, every key commented. `values-dev.yaml`, `values-staging.yaml`, `values-prod.yaml` hold only the overrides.
- `values.schema.json`: the chart refuses to render without `resources.requests` and `resources.limits` (cpu and memory), without probes, or without `database.existingSecret` (unless `database.createSecret` is true with a `connectionString`).
- `templates/`: `deployment.yaml`, `service.yaml` (always `type: ClusterIP`), `ingress.yaml` (only when `ingress.enabled`), `secret.yaml` (only when `database.createSecret`), `serviceaccount.yaml`, `hpa.yaml` and `pdb.yaml` (behind `enabled` flags, on in prod), `NOTES.txt`, `_helpers.tpl`.

## How the Deployment works

- Probes: `startupProbe` and `livenessProbe` hit `/health/live` (no checks, so a database outage never restarts pods); `readinessProbe` hits `/health/ready` (includes the database check tagged `ready` in `Program.cs`). The startup probe allows 150 seconds for migrations.
- The connection string comes from a Secret via `secretKeyRef` into `ConnectionStrings__CustomerDb`.
- `Database__ApplyMigrationsOnStartup` is on everywhere. Concurrent replicas are safe: EF Core's migration lock serialises migrations, and `MigrateDatabaseAsync` in `Program.cs` retries SQL error 1801 while another replica is creating the database.
- Pod security: runs as UID 1654 (the .NET image's `app` user), read-only root filesystem with an `emptyDir` at `/tmp`, no privilege escalation, all capabilities dropped, `RuntimeDefault` seccomp, no service account token.
- `replicas` is omitted when `autoscaling.enabled`, so the HPA owns the count.
- `topologySpread.enabled` generates constraints from `topologyKeys` using the chart's selector labels.

## Change the chart (API chart; the web chart works the same way with its own paths)

1. Add the value to `values.yaml` with a comment and a safe default. Override it only in the environment files that differ.
2. Use it in the template. Lists and maps go through `toYaml ... | nindent N`; scalars that may look numeric or boolean get `| quote`.
3. If it must always be set, add it to `values.schema.json`.
4. Check every environment:
   ```bash
   helm lint apps/api/helm/customer-service -f apps/api/helm/customer-service/values-dev.yaml
   helm template customer-service apps/api/helm/customer-service -f apps/api/helm/customer-service/values-prod.yaml
   sh .claude/pre-pr-checks       # lints and renders every chart under apps/*/helm/* for all three environments
   ```
5. Bump `version` in `Chart.yaml` for any chart change, and `appVersion` when the default image changes.

## Add an environment

Copy the closest `values-<env>.yaml`, adjust replicas, resources, ingress host and `aspnetcoreEnvironment`, and add the name to the `for env in ...` loop in `.claude/pre-pr-checks`. Do it in both charts; the loop checks every chart for every environment name.

## Web chart (`apps/web/helm/customer-service-web`)

- Layout mirrors the API chart: `values.yaml` plus `values-dev.yaml`, `values-staging.yaml`, `values-prod.yaml`; `values.schema.json`; templates `deployment.yaml`, `service.yaml` (`ClusterIP`, port 80 to container port 3000), `ingress.yaml`, `serviceaccount.yaml`, `hpa.yaml`, `pdb.yaml`, `NOTES.txt`. There's no Secret: the app holds no credentials.
- The schema requires `image.repository`, `apiBaseUrl` (an `http(s)://` URL), cpu and memory requests and limits, and all three probes.
- Env set by the Deployment: `PORT`, `HOSTNAME=0.0.0.0`, `NEXT_TELEMETRY_DISABLED`, and `API_BASE_URL` from `apiBaseUrl`. Add others through `extraEnv` (Kubernetes EnvVar objects).
- `apiBaseUrl` defaults to `http://customer-service`, the API chart's `ClusterIP` Service (port 80) in the same namespace. Override with `--set apiBaseUrl=...` if the API release has another name or namespace.
- Probes: startup, liveness and readiness all hit `/api/health`, which never calls the API, so an API outage doesn't restart or unready web pods. Startup allows 60 seconds.
- Pod security: UID/GID 1001 (the user created in `apps/web/Dockerfile`), read-only root filesystem, `emptyDir` volumes at `/tmp` and `/app/.next/cache`, no privilege escalation, all capabilities dropped, `RuntimeDefault` seccomp, no service account token. If you change the Dockerfile's user or the cache path, change the chart to match.
- Environments: dev 1 replica and no ingress; staging 2 replicas and ingress on; prod HPA 3 to 10, PDB (`minAvailable: 2`), topology spread and ingress on.
- The `version` bump rule is the same as for the API chart.

## Install order and ingress

Install the API chart first, then the web chart. The browser only talks to the web app, so the web chart's ingress owns the public host and the API chart's ingress is off in dev, staging and prod (turn it on with `--set ingress.enabled=true` only when you need external access to the API).

```bash
helm upgrade --install customer-service apps/api/helm/customer-service -n <ns> \
  -f apps/api/helm/customer-service/values-<env>.yaml --set image.repository=<registry>/customer-service-api --set image.tag=<tag>
helm upgrade --install customer-service-web apps/web/helm/customer-service-web -n <ns> \
  -f apps/web/helm/customer-service-web/values-<env>.yaml --set image.repository=<registry>/customer-service-web --set image.tag=<tag>
```

In dev (no ingress): `kubectl port-forward svc/customer-service-web 3000:80 -n <ns>`.

## Try it on a local kind cluster (Podman)

```bash
KIND_EXPERIMENTAL_PROVIDER=podman kind create cluster --name customer-service-test
podman build -t localhost/customer-service-api:dev apps/api
podman save -o /tmp/api.tar localhost/customer-service-api:dev
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/api.tar --name customer-service-test
```

SQL Server's image is amd64-only: `podman save` it and `kind load image-archive` it the same way, and run it with `imagePullPolicy: Never` (the node can't pull an amd64 image itself). Then create the `customer-service-db` Secret and install with `--set image.repository=localhost/customer-service-api --set image.tag=dev --set image.pullPolicy=Never`. Delete the cluster afterwards with `kind delete cluster --name customer-service-test`.

## Debugging

- `CreateContainerConfigError`: the Secret named in `database.existingSecret` doesn't exist, or lacks the `database.secretKey` key.
- Pods Running but not Ready: `/health/ready` is failing, so the database is unreachable. Check the Secret's connection string and `kubectl logs`.
- Pods restarting with exit code 1: a start-up failure, logged at `crit` as "Host terminated unexpectedly". The app exits on purpose rather than hanging as PID 1.
- Web pods Ready but every page shows the error screen: the API Service isn't answering at `apiBaseUrl`. Check `kubectl get svc` for the API Service name and port 80, and `kubectl logs` on a web pod (an unset `API_BASE_URL` is reported there too).
- Web pods in `CrashLoopBackOff` with permission errors: the image user no longer matches UID 1001 in `podSecurityContext`, or `/app/.next/cache` lost its `emptyDir`.
