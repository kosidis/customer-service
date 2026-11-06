---
name: helm
description: WHEN changing the Helm chart in deploy/helm/customer-service, adding a value or environment, or deploying and debugging the service on Kubernetes.
---

# Helm chart

Chart: `deploy/helm/customer-service` (`apiVersion: v2`, chart `version` 0.1.0, `appVersion` 1.0.0). Built and tested with Helm 4.

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

## Change the chart

1. Add the value to `values.yaml` with a comment and a safe default. Override it only in the environment files that differ.
2. Use it in the template. Lists and maps go through `toYaml ... | nindent N`; scalars that may look numeric or boolean get `| quote`.
3. If it must always be set, add it to `values.schema.json`.
4. Check every environment:
   ```bash
   helm lint deploy/helm/customer-service -f deploy/helm/customer-service/values-dev.yaml
   helm template customer-service deploy/helm/customer-service -f deploy/helm/customer-service/values-prod.yaml
   sh .claude/pre-pr-checks       # lints and renders all three
   ```
5. Bump `version` in `Chart.yaml` for any chart change, and `appVersion` when the default image changes.

## Add an environment

Copy the closest `values-<env>.yaml`, adjust replicas, resources, ingress host and `aspnetcoreEnvironment`, and add the name to the `for env in ...` loop in `.claude/pre-pr-checks`.

## Try it on a local kind cluster (Podman)

```bash
KIND_EXPERIMENTAL_PROVIDER=podman kind create cluster --name customer-service-test
podman build -t localhost/customer-service-api:dev .
podman save -o /tmp/api.tar localhost/customer-service-api:dev
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/api.tar --name customer-service-test
```

SQL Server's image is amd64-only: `podman save` it and `kind load image-archive` it the same way, and run it with `imagePullPolicy: Never` (the node can't pull an amd64 image itself). Then create the `customer-service-db` Secret and install with `--set image.repository=localhost/customer-service-api --set image.tag=dev --set image.pullPolicy=Never`. Delete the cluster afterwards with `kind delete cluster --name customer-service-test`.

## Debugging

- `CreateContainerConfigError`: the Secret named in `database.existingSecret` doesn't exist, or lacks the `database.secretKey` key.
- Pods Running but not Ready: `/health/ready` is failing, so the database is unreachable. Check the Secret's connection string and `kubectl logs`.
- Pods restarting with exit code 1: a start-up failure, logged at `crit` as "Host terminated unexpectedly". The app exits on purpose rather than hanging as PID 1.
