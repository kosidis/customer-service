#!/usr/bin/env bash
set -euo pipefail

# ---- Required input (export these before running) ----
: "${ANTHROPIC_API_KEY:?export ANTHROPIC_API_KEY first}"
: "${TEAM_NAME:?export TEAM_NAME, e.g. platform-team}"
: "${TEAM_KEY:?export TEAM_KEY, must start with sk-}"

# ---- Optional ----
LITELLM_MASTER_KEY="${LITELLM_MASTER_KEY:-sk-master-$(openssl rand -hex 16)}"
TEAM_ID="${TEAM_ID:-$TEAM_NAME}"
TEAM_MODELS='["opus","sonnet","haiku"]'
URL="http://localhost:4000"

if [[ "$TEAM_KEY" != sk-* ]]; then
  echo "TEAM_KEY must start with 'sk-'" >&2; exit 1
fi

cd "$(dirname "$0")"

echo "==> Creating kind cluster"
kind get clusters | grep -qx litellm || kind create cluster --config kind-config.yaml

echo "==> Installing Traefik"
helm repo add traefik https://traefik.github.io/charts >/dev/null 2>&1 || true
helm repo update traefik >/dev/null
helm upgrade --install traefik traefik/traefik \
  --namespace traefik --create-namespace \
  -f traefik-values.yaml --wait --timeout 5m

echo "==> Creating namespace and secret"
kubectl create namespace litellm --dry-run=client -o yaml | kubectl apply -f -
kubectl -n litellm create secret generic litellm-secrets \
  --from-literal=ANTHROPIC_API_KEY="$ANTHROPIC_API_KEY" \
  --from-literal=LITELLM_MASTER_KEY="$LITELLM_MASTER_KEY" \
  --from-literal=DATABASE_URL="postgresql://litellm:litellm@postgres:5432/litellm" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "==> Deploying Postgres + LiteLLM"
kubectl apply -f litellm.yaml
kubectl -n litellm rollout status deploy/postgres --timeout=180s
kubectl -n litellm rollout status deploy/litellm  --timeout=300s

echo "==> Applying ingress (litellm.localhost)"
kubectl apply -f ingress.yaml

echo "==> Waiting for LiteLLM API"
until curl -sf "$URL/health/readiness" >/dev/null; do sleep 3; done

echo "==> Creating team '$TEAM_NAME'"
curl -sS -X POST "$URL/team/new" \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d "{\"team_id\":\"$TEAM_ID\",\"team_alias\":\"$TEAM_NAME\",\"models\":$TEAM_MODELS}"
echo

echo "==> Creating key for team"
curl -sS -X POST "$URL/key/generate" \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d "{\"key\":\"$TEAM_KEY\",\"team_id\":\"$TEAM_ID\",\"key_alias\":\"$TEAM_NAME-key\",\"models\":$TEAM_MODELS}"
echo

cat <<EOF

Done.
  LiteLLM (ingress): http://litellm.localhost   (UI: http://litellm.localhost/ui)
  LiteLLM (direct):  $URL
  Master key:  $LITELLM_MASTER_KEY
  Team:        $TEAM_NAME (id: $TEAM_ID)
  Team key:    $TEAM_KEY

Test:
  curl http://litellm.localhost/v1/chat/completions \\
    -H "Authorization: Bearer $TEAM_KEY" -H "Content-Type: application/json" \\
    -d '{"model":"sonnet","messages":[{"role":"user","content":"Hello"}]}'
EOF
