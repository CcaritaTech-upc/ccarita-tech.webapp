#!/usr/bin/env bash
set -euo pipefail
export DOTNET_ROOT=/home/arroz/.dotnet
export PATH="$DOTNET_ROOT:$PATH"
root="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$root"
suffix="$(date +%s)-$$"; container="iobuild-pr3proof-mysql-$suffix"; network="iobuild-pr3proof-net-$suffix"; port=$((32000 + RANDOM % 1000)); api=$((5100 + RANDOM % 200)); stripe_port=$((5400 + RANDOM % 200)); log="/tmp/iobuild-pr3proof-$suffix.log"; pid=""; stripe_pid=""
cleanup(){
  trap - EXIT
  for process in "$pid" "$stripe_pid"; do if [[ -n "$process" ]] && kill -0 "$process" 2>/dev/null; then kill "$process" 2>/dev/null || true; wait "$process" 2>/dev/null || true; fi; done
  sudo -n -u hermes docker rm -f "$container" >/dev/null 2>&1 || true
  sudo -n -u hermes docker network rm "$network" >/dev/null 2>&1 || true
}
trap cleanup EXIT
start(){ ConnectionStrings__IoBuild="$conn" Migrations__ApplyOnStartup=true Stripe__WebhookSecret="${1:-}" Stripe__RestrictedApiKey="${2:-}" Stripe__ProviderBaseUrl="http://127.0.0.1:$stripe_port/" Stripe__PlanPrices__3=price_runtime Stripe__BuilderCustomers__1=cus_runtime dotnet run --no-build --project backend/src/IoBuild.Api --urls "http://127.0.0.1:$api" >"$log" 2>&1 & pid=$!; for _ in $(seq 1 60); do curl -fsS "http://127.0.0.1:$api/health" >/dev/null 2>&1 && return; sleep 1; done; cat "$log"; return 1; }
status(){ local body; body=$(mktemp); local code; code=$(curl -sS -o "$body" -w '%{http_code}' "$@"); printf '%s|%s' "$code" "$(cat "$body")"; rm -f "$body"; }
sign(){ PAYLOAD="$1" T="$2" python3 - <<'PY'
import hashlib,hmac,os
print('t='+os.environ['T']+',v1='+hmac.new(b'whsec_runtime',(os.environ['T']+'.'+os.environ['PAYLOAD']).encode(),hashlib.sha256).hexdigest())
PY
}
python3 backend/tests/Integration/fake_stripe_server.py "$stripe_port" >/tmp/iobuild-pr3proof-stripe-$suffix.log 2>&1 & stripe_pid=$!
for _ in $(seq 1 30); do curl -fsS "http://127.0.0.1:$stripe_port/health" >/dev/null 2>&1 && break; sleep 1; done
curl -fsS "http://127.0.0.1:$stripe_port/health" >/dev/null
sudo -n -u hermes docker network create "$network" >/dev/null
sudo -n -u hermes docker run -d --name "$container" --network "$network" -p "127.0.0.1:$port:3306" -e MYSQL_ROOT_PASSWORD=iobuild -e MYSQL_DATABASE=iobuild mysql:8.0 >/dev/null
for _ in $(seq 1 60); do sudo -n -u hermes docker exec "$container" mysqladmin ping -h 127.0.0.1 -uroot -piobuild --silent >/dev/null 2>&1 && break; sleep 1; done
conn="Server=127.0.0.1;Port=$port;Database=iobuild;User=root;Password=iobuild"
payload='{"id":"evt_paid","type":"checkout.session.completed","data":{"object":{"payment_status":"paid","metadata":{"builder_id":"1","plan_id":"3"}}}}'
start
missing="$(status -H "Stripe-Signature: $(sign "$payload" "$(date +%s)")" -H 'content-type: application/json' -d "$payload" "http://127.0.0.1:$api/api/v1/webhooks/stripe")"; [[ ${missing%%|*} == 401 ]]
kill "$pid"; wait "$pid" || true; pid=""
start whsec_runtime rk_test_minimum
reg="$(status -H 'content-type: application/json' -d '{"email":"proof@example.test","password":"secret","role":"Builder"}' "http://127.0.0.1:$api/api/v1/users")"; [[ ${reg%%|*} == 201 ]]
token=$(status -H 'content-type: application/json' -d '{"email":"proof@example.test","password":"secret"}' "http://127.0.0.1:$api/api/v1/sessions" | cut -d'|' -f2 | python3 -c 'import json,sys;print(json.load(sys.stdin)["token"])')
auth="Authorization: Bearer $token"
project="$(status -H "$auth" -H 'content-type: application/json' -d '{"name":"P","description":"D","location":"L","totalUnits":1,"builderId":1,"imageUrl":null}' "http://127.0.0.1:$api/api/v1/projects")"; [[ ${project%%|*} == 201 ]]
profile="$(status -H "$auth" -H 'content-type: application/json' -d '{"userId":1,"name":"N","username":"u"}' "http://127.0.0.1:$api/api/v1/profiles")"; [[ ${profile%%|*} == 201 ]]
subscription="$(status -H "$auth" -H 'content-type: application/json' -d '{"builderId":1,"planId":3,"startDate":"2026-01-01T00:00:00Z","endDate":null}' "http://127.0.0.1:$api/api/v1/subscriptions")"; [[ ${subscription%%|*} == 201 ]]
checkout="$(status -H 'content-type: application/json' -d '{"builderId":1,"planId":3,"successUrl":"https://success.example","cancelUrl":"https://cancel.example"}' "http://127.0.0.1:$api/api/v1/subscriptions/payments/sessions")"; [[ ${checkout%%|*} == 201 ]]; grep -q 'cs_runtime' <<<"${checkout#*|}"; ! grep -q 'rk_test_minimum' <<<"${checkout#*|}"
confirmation="$(status -X PATCH "http://127.0.0.1:$api/api/v1/subscriptions/payments/sessions/cs_runtime")"; [[ ${confirmation%%|*} == 200 ]]; grep -q '"status":"paid"' <<<"${confirmation#*|}"
invoices="$(status "http://127.0.0.1:$api/api/v1/subscriptions/payments/invoices?builderId=1")"; [[ ${invoices%%|*} == 200 ]]; grep -q 'in_runtime' <<<"${invoices#*|}"
stripe_proof="$(curl -fsS "http://127.0.0.1:$stripe_port/proof")"; [[ "$stripe_proof" == '{"checkout": 1, "session": 1, "invoices": 1}' ]]
now=$(date +%s); stale="$(status -H "Stripe-Signature: $(sign "$payload" 1)" -H 'content-type: application/json' -d "$payload" "http://127.0.0.1:$api/api/v1/webhooks/stripe")"; [[ ${stale%%|*} == 401 ]]
other='{"id":"evt_other","type":"customer.created","data":{"object":{}}}'; unrelated="$(status -H "Stripe-Signature: $(sign "$other" "$now")" -H 'content-type: application/json' -d "$other" "http://127.0.0.1:$api/api/v1/webhooks/stripe")"; [[ ${unrelated%%|*} == 200 ]]
unpaid='{"id":"evt_unpaid","type":"checkout.session.completed","data":{"object":{"payment_status":"unpaid","metadata":{"builder_id":"1","plan_id":"3"}}}}'; unpaid_result="$(status -H "Stripe-Signature: $(sign "$unpaid" "$now")" -H 'content-type: application/json' -d "$unpaid" "http://127.0.0.1:$api/api/v1/webhooks/stripe")"; [[ ${unpaid_result%%|*} == 200 ]]
paid="$(status -H "Stripe-Signature: $(sign "$payload" "$now")" -H 'content-type: application/json' -d "$payload" "http://127.0.0.1:$api/api/v1/webhooks/stripe")"; duplicate="$(status -H "Stripe-Signature: $(sign "$payload" "$now")" -H 'content-type: application/json' -d "$payload" "http://127.0.0.1:$api/api/v1/webhooks/stripe")"; [[ ${paid%%|*}:${duplicate%%|*} == 200:200 ]]
counts=$(sudo -n -u hermes docker exec "$container" mysql -N -uroot -piobuild iobuild -e 'SELECT CONCAT((SELECT COUNT(*) FROM projects),":",(SELECT COUNT(*) FROM profiles),":",(SELECT COUNT(*) FROM subscriptions),":",(SELECT COUNT(*) FROM subscription_webhooks),":",(SELECT COUNT(*) FROM __EFMigrationsHistory));' | tr -d '\r'); [[ "$counts" == 1:1:2:3:3 ]]
kill "$pid"; wait "$pid" || true; pid=""; start whsec_runtime rk_test_minimum
history=$(sudo -n -u hermes docker exec "$container" mysql -N -uroot -piobuild iobuild -e 'SELECT COUNT(*) FROM __EFMigrationsHistory;' | tr -d '\r'); [[ "$history" == 3 ]]
printf 'PASS missing=%s checkout=%s confirmation=%s invoices=%s stripe=%s stale=%s unrelated=%s unpaid=%s paid=%s duplicate=%s counts=%s migrations=%s resources=%s,%s\n' "${missing%%|*}" "$checkout" "$confirmation" "$invoices" "$stripe_proof" "${stale%%|*}" "${unrelated%%|*}" "${unpaid_result%%|*}" "${paid%%|*}" "${duplicate%%|*}" "$counts" "$history" "$container" "$network"
