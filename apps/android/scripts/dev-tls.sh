#!/bin/sh
# Generates a local dev CA and a server certificate for the Compose tls-proxy
# and the kind ingress. Run from anywhere; paths resolve from the repo root.
#
# Idempotent: an existing CA (dev-certs/ca.crt + ca.key) is REUSED so the CA
# bundled into the Android debug build stays valid. The server certificate is
# always regenerated (cheap, picks up hostname changes). Delete dev-certs/ to
# rotate the CA, then rebuild the debug app.
#
# Output (dev-certs/ is gitignored, keys are mode 600, no key is ever printed):
#   dev-certs/ca.crt, ca.key          local CA (key never leaves dev-certs/)
#   dev-certs/server.crt, server.key  mounted by tls-proxy, used by the kind secret
#   apps/android/app/src/debug/res/raw/dev_ca.pem   public CA cert only (gitignored)
set -eu

ROOT=$(cd "$(dirname "$0")/../../.." && pwd)
CERTS="$ROOT/dev-certs"
RAW="$ROOT/apps/android/app/src/debug/res/raw"
LAN_NAME=$(hostname)
CA_DAYS=3650    # CA: 10 years
LEAF_DAYS=397   # server leaf: under the 398-day limit

command -v openssl >/dev/null 2>&1 || { echo "openssl is required" >&2; exit 1; }

umask 077
mkdir -p "$CERTS"

if [ -s "$CERTS/ca.key" ] && [ -s "$CERTS/ca.crt" ]; then
  echo "Reusing existing CA in dev-certs/"
else
  echo "Creating CA"
  openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days "$CA_DAYS" \
    -keyout "$CERTS/ca.key" -out "$CERTS/ca.crt" \
    -subj "/CN=Customer Service Dev CA" \
    -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
    -addext "keyUsage=critical,keyCertSign,cRLSign" 2>/dev/null
fi

EXT=$(mktemp)
CSR=$(mktemp)
trap 'rm -f "$EXT" "$CSR"' EXIT
cat > "$EXT" <<EXTEOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:localhost,IP:10.0.2.2,DNS:$LAN_NAME,DNS:customer-api.localhost,DNS:api.10-0-2-2.nip.io
EXTEOF

echo "Creating server certificate (SANs: localhost, 10.0.2.2, $LAN_NAME, customer-api.localhost, api.10-0-2-2.nip.io)"
openssl req -new -newkey rsa:2048 -nodes -sha256 \
  -keyout "$CERTS/server.key" -out "$CSR" -subj "/CN=localhost" 2>/dev/null
openssl x509 -req -sha256 -days "$LEAF_DAYS" -in "$CSR" \
  -CA "$CERTS/ca.crt" -CAkey "$CERTS/ca.key" -CAcreateserial \
  -extfile "$EXT" -out "$CERTS/server.crt" 2>/dev/null

# Keys stay owner-only; nginx's master process reads them as root in the container.
chmod 600 "$CERTS/ca.key" "$CERTS/server.key"
chmod 644 "$CERTS/ca.crt" "$CERTS/server.crt"

mkdir -p "$RAW"
cp "$CERTS/ca.crt" "$RAW/dev_ca.pem"
chmod 644 "$RAW/dev_ca.pem"

echo "Wrote dev-certs/ca.crt, server.crt, server.key and $RAW/dev_ca.pem"
echo
echo "For kind (not run), create the ingress TLS secret with:"
echo "  kubectl create secret tls customer-service-api-tls --cert=dev-certs/server.crt --key=dev-certs/server.key -n <namespace>"
