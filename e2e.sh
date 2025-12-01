#!/usr/bin/env bash
set -euo pipefail

endpoint="$(terraform -chdir=infra output -raw api_endpoint)"
identity="e2e-$(date +%s)"

fail() { echo "e2e: FAILED: $*" >&2; exit 1; }
post() { curl -fsS -X POST "$endpoint/identity/$1" -H 'Content-Type: application/json' -d "{\"address\":\"$2\"}"; }

body=""
for attempt in 1 2 3 4 5; do
  if body="$(post "$identity" 203.0.113.10 2>&1)"; then break; fi
  echo "e2e: attempt $attempt failed: $body" >&2
  sleep 3
done
grep -q '"created":true' <<<"$body" || fail "first report did not create: $body"
echo "e2e: create -> $body"

body="$(curl -fsS "$endpoint/identity/$identity")"
grep -q '"address":"203.0.113.10"' <<<"$body" || fail "lookup returned wrong address: $body"
changed="$(grep -o '"last_changed":"[^"]*"' <<<"$body")"
echo "e2e: lookup -> $body"

body="$(post "$identity" 203.0.113.10)"
grep -q '"created":false' <<<"$body" || fail "repeat report did not update: $body"
grep -qF "$changed" <<<"$body" || fail "repeat report moved last_changed: $body"
echo "e2e: same address -> $body"

body="$(post "$identity" 203.0.113.11)"
if grep -qF "$changed" <<<"$body"; then fail "new address did not move last_changed: $body"; fi
echo "e2e: new address -> $body"

code="$(curl -s -o /dev/null -w '%{http_code}' "$endpoint/identity/$identity-missing")"
[ "$code" = "404" ] || fail "unknown identity returned $code"
echo "e2e: unknown identity -> 404"

client="$(REGISTRY_ENDPOINT="$endpoint" IDENTITY="$identity-client" ADDRESS=203.0.113.12 INTERVAL_SECONDS=2 \
  timeout 10 dotnet ReportingClient/src/ReportingClient/bin/Debug/net8.0/ReportingClient.dll || true)"
echo "$client"
grep -q 'result=created' <<<"$client" || fail "client never created its entry"
grep -q 'result=updated' <<<"$client" || fail "client never reported an update"

echo "e2e: ALL CHECKS PASSED"
