#!/usr/bin/env bash
set -euo pipefail
export DOTNET_ROOT=/home/arroz/.dotnet
export PATH="$DOTNET_ROOT:$PATH"
root="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$root"
suffix="$(date +%s)-$$"
network="iobuild-wu4-net-$suffix"
mysql="iobuild-wu4-mysql-$suffix"
mosquitto="iobuild-wu4-mosquitto-$suffix"
influx="iobuild-wu4-influx-$suffix"
simulator="iobuild-wu4-simulator-$suffix"
simulator_image="iobuild-legacy-simulator:latest"
mysql_port=$((33000 + RANDOM % 800))
mqtt_port=$((34000 + RANDOM % 800))
influx_port=$((35000 + RANDOM % 800))
api_port=$((5600 + RANDOM % 200))
api_pid=""
api_log="/tmp/iobuild-wu4-api-$suffix.log"
simulator_stage="/tmp/iobuild-wu4-simulator-source-$suffix"
cleanup() {
  trap - EXIT
  for process in "$api_pid"; do
    if [[ -n "$process" ]] && kill -0 "$process" 2>/dev/null; then kill "$process" 2>/dev/null || true; wait "$process" 2>/dev/null || true; fi
  done
  sudo -n -u hermes docker rm -f "$mysql" "$mosquitto" "$influx" "$simulator" >/dev/null 2>&1 || true
  sudo -n -u hermes docker network rm "$network" >/dev/null 2>&1 || true
  rm -rf "$simulator_stage"
}
trap cleanup EXIT
status() { local body code; body=$(mktemp); code=$(curl -sS -o "$body" -w '%{http_code}' "$@"); printf '%s|%s' "$code" "$(cat "$body")"; rm -f "$body"; }
wait_http() { local url="$1"; for _ in $(seq 1 90); do curl -fsS "$url" >/dev/null 2>&1 && return; sleep 1; done; return 1; }
start_api() {
  ConnectionStrings__IoBuild="$connection" Migrations__ApplyOnStartup=true Mqtt__Enabled=true Mqtt__Host=127.0.0.1 Mqtt__Port="$mqtt_port" Influx__Url="http://127.0.0.1:$influx_port" Influx__Org=iobuild Influx__Bucket=telemetry Influx__Token=wu4-token \
    dotnet run --no-build --project backend/src/IoBuild.Api --urls "http://127.0.0.1:$api_port" >"$api_log" 2>&1 &
  api_pid=$!
  if ! wait_http "http://127.0.0.1:$api_port/health"; then cat "$api_log"; return 1; fi
}
retained_payload() {
  sudo -n -u hermes docker exec "$mosquitto" mosquitto_sub -h localhost -t "$1" -C 1 -W 10
}
sudo -n -u hermes docker network create "$network" >/dev/null
sudo -n -u hermes docker run -d --name "$mysql" --network "$network" -p "127.0.0.1:$mysql_port:3306" -e MYSQL_ROOT_PASSWORD=iobuild -e MYSQL_DATABASE=iobuild mysql:8.0 >/dev/null
sudo -n -u hermes docker run -d --name "$mosquitto" --network "$network" -p "127.0.0.1:$mqtt_port:1883" eclipse-mosquitto:2 >/dev/null
sudo -n -u hermes docker run -d --name "$influx" --network "$network" -p "127.0.0.1:$influx_port:8086" \
  -e DOCKER_INFLUXDB_INIT_MODE=setup -e DOCKER_INFLUXDB_INIT_USERNAME=iobuild -e DOCKER_INFLUXDB_INIT_PASSWORD=iobuild-pass -e DOCKER_INFLUXDB_INIT_ORG=iobuild -e DOCKER_INFLUXDB_INIT_BUCKET=telemetry -e DOCKER_INFLUXDB_INIT_ADMIN_TOKEN=wu4-token influxdb:2.7 >/dev/null
for _ in $(seq 1 90); do sudo -n -u hermes docker exec "$mysql" mysqladmin ping -h 127.0.0.1 -uroot -piobuild --silent >/dev/null 2>&1 && break; sleep 1; done
sudo -n -u hermes docker exec "$mysql" mysqladmin ping -h 127.0.0.1 -uroot -piobuild --silent >/dev/null
wait_http "http://127.0.0.1:$influx_port/health"
connection="Server=127.0.0.1;Port=$mysql_port;Database=iobuild;User=root;Password=iobuild"
# hermes owns Docker but cannot traverse /home/arroz; stage an exact read-only source copy
# outside the private home before the Docker build and remove it in the EXIT cleanup.
cp -a /home/arroz/dev_projects/iobuild/microservices/microservices/iot-simulator "$simulator_stage"
if ! sudo -n -u hermes docker image inspect "$simulator_image" >/dev/null 2>&1; then sudo -n -u hermes docker build -t "$simulator_image" "$simulator_stage" >/dev/null; fi
sudo -n -u hermes docker run -d --name "$simulator" --network "$network" -e MQTT_HOST="$mosquitto" "$simulator_image" >/dev/null
for _ in $(seq 1 20); do sudo -n -u hermes docker logs "$simulator" 2>&1 | grep -q 'Subscribed to registry/#' && break; sleep 1; done
sudo -n -u hermes docker logs "$simulator" 2>&1 | grep -q 'Subscribed to registry/#'
start_api
types="$(status "http://127.0.0.1:$api_port/api/v1/devices/types")"; [[ ${types%%|*} == 200 ]]
python3 - "${types#*|}" <<'PY'
import json
import sys

catalog = json.loads(sys.argv[1])
entries = {entry["code"]: entry for entry in catalog["deviceTypes"]}
assert entries["SmartLight"]["displayName"] == "Smart Light"
assert entries["SmartLight"]["scope"] == "unit"
assert [attribute["name"] for attribute in entries["SmartLight"]["controllableAttributes"]] == ["brightness", "power"]
PY
anonymous_devices="$(status "http://127.0.0.1:$api_port/api/v1/devices")"; [[ ${anonymous_devices%%|*} == 401 ]]
registration="$(status -H 'content-type: application/json' -d '{"email":"devices-proof@example.test","password":"secret","role":"Builder"}' "http://127.0.0.1:$api_port/api/v1/users")"; [[ ${registration%%|*} == 201 ]]
token=$(status -H 'content-type: application/json' -d '{"email":"devices-proof@example.test","password":"secret"}' "http://127.0.0.1:$api_port/api/v1/sessions" | cut -d'|' -f2 | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')
auth="Authorization: Bearer $token"
owner_registration="$(status -H 'content-type: application/json' -d '{"email":"owner-proof@example.test","password":"secret","role":"Owner"}' "http://127.0.0.1:$api_port/api/v1/users")"; [[ ${owner_registration%%|*} == 201 ]]
owner_token=$(status -H 'content-type: application/json' -d '{"email":"owner-proof@example.test","password":"secret"}' "http://127.0.0.1:$api_port/api/v1/sessions" | cut -d'|' -f2 | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')
owner_auth="Authorization: Bearer $owner_token"
sudo -n -u hermes docker exec "$mysql" mysql -uroot -piobuild iobuild -e "INSERT INTO unit_owner_projections (UnitId, OwnerUserId, UpdatedAt) VALUES (7, 2, UTC_TIMESTAMP(6));"
missing_status="$(status -H "$auth" "http://127.0.0.1:$api_port/api/v1/devices/999/status")"; [[ ${missing_status%%|*} == 404 ]]; grep -q 'Device with ID 999 not found' <<<"${missing_status#*|}"
missing_energy="$(status -H "$auth" "http://127.0.0.1:$api_port/api/v1/devices/999/energy")"; [[ ${missing_energy%%|*} == 404 ]]; grep -q 'Device with ID 999 not found' <<<"${missing_energy#*|}"
project="$(status -H "$auth" -H 'content-type: application/json' -d '{"name":"Devices","description":"Runtime","location":"Lima","totalUnits":1,"builderId":1,"imageUrl":null}' "http://127.0.0.1:$api_port/api/v1/projects")"; [[ ${project%%|*} == 201 ]]
device="$(status -H "$auth" -H 'content-type: application/json' -d '{"name":"Runtime Light","type":"SmartLight","location":"Unit 1","macAddress":"01:02:03:04:05:06","projectId":1,"status":"online"}' "http://127.0.0.1:$api_port/api/v1/devices")"; [[ ${device%%|*} == 201 ]]; device_id=$(printf '%s' "${device#*|}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')

# Characterize unit-scoped owner-custom creation, catalog scope enforcement, and duplicate conflict bodies.
owner_custom="$(status -H "$owner_auth" -H 'content-type: application/json' -d '{"name":"Unit Light","type":"SmartLight","location":"Unit 7","macAddress":"aa:bb:cc:dd:ee:ff","projectId":1,"status":"online","unitId":7}' "http://127.0.0.1:$api_port/api/v1/devices")"; [[ ${owner_custom%%|*} == 201 ]]
python3 - "${owner_custom#*|}" <<'PY2'
import json, sys
body = json.loads(sys.argv[1])
assert set(body) == {"id", "name", "type", "location", "macAddress", "projectId", "status"}
assert body["macAddress"] is None
PY2
duplicate_type="$(status -H "$owner_auth" -H 'content-type: application/json' -d '{"name":"Second Unit Light","type":"SmartLight","location":"Unit 7","macAddress":null,"projectId":1,"status":"online","unitId":7}' "http://127.0.0.1:$api_port/api/v1/devices")"; [[ ${duplicate_type%%|*} == 409 ]]; grep -q 'A device of this type already exists in this unit.' <<<"${duplicate_type#*|}"
floor_type="$(status -H "$owner_auth" -H 'content-type: application/json' -d '{"name":"Wrong Scope","type":"SmartMeter","location":"Unit 7","macAddress":null,"projectId":1,"status":"online","unitId":7}' "http://127.0.0.1:$api_port/api/v1/devices")"; [[ ${floor_type%%|*} == 400 ]]; grep -q 'cannot be added to a unit' <<<"${floor_type#*|}"
duplicate_mac="$(status -H "$owner_auth" -H 'content-type: application/json' -d '{"name":"Duplicate MAC","type":"SmartLight","location":"Unit 8","macAddress":"01:02:03:04:05:06","projectId":1,"status":"online"}' "http://127.0.0.1:$api_port/api/v1/devices")"; [[ ${duplicate_mac%%|*} == 409 ]]; grep -q 'A device with the same MAC address already exists.' <<<"${duplicate_mac#*|}"

command_device_id=$(printf '%s' "${owner_custom#*|}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')
# Builder cannot actuate a unit-owned device; Owner can only after ownership projection exists.
builder_command="$(status -H "$auth" -H 'content-type: application/json' -d '{"attribute":"brightness","value":80}' "http://127.0.0.1:$api_port/api/v1/devices/$command_device_id/commands")"; [[ ${builder_command%%|*} == 403 ]]; grep -q 'Only unit owners may send device commands' <<<"${builder_command#*|}"
for _ in $(seq 1 20); do sudo -n -u hermes docker logs "$simulator" 2>&1 | grep -q "device $command_device_id registered" && break; sleep 1; done
sudo -n -u hermes docker logs "$simulator" 2>&1 | grep -q "device $command_device_id registered"
registry=$(retained_payload "registry/$command_device_id")
python3 - "$command_device_id" "$registry" <<'PY'
import json
import sys

expected_device_id = int(sys.argv[1])
payload = json.loads(sys.argv[2])
assert payload["deviceId"] == expected_device_id, payload
assert payload["type"] == "SmartLight", payload
PY
command="$(status -H "$owner_auth" -H 'content-type: application/json' -d '{"attribute":"brightness","value":80}' "http://127.0.0.1:$api_port/api/v1/devices/$command_device_id/commands")"; [[ ${command%%|*} == 200 ]]
retained=$(retained_payload "commands/$command_device_id"); grep -q '"brightness":80' <<<"$retained"
for _ in $(seq 1 30); do telemetry_count=$(sudo -n -u hermes docker exec "$mysql" mysql -N -uroot -piobuild iobuild -e "SELECT COUNT(*) FROM device_telemetry WHERE DeviceId=$command_device_id;" | tr -d '\r'); [[ "$telemetry_count" -gt 0 ]] && break; sleep 1; done
[[ "$telemetry_count" -gt 0 ]]
status_body="$(status -H "$auth" "http://127.0.0.1:$api_port/api/v1/devices/$command_device_id/status")"; [[ ${status_body%%|*} == 200 ]]; grep -q '"brightness":80' <<<"${status_body#*|}"
energy_body="$(status -H "$auth" "http://127.0.0.1:$api_port/api/v1/devices/$command_device_id/energy?from=2020-01-01T00:00:00Z&to=2030-01-01T00:00:00Z")"; [[ ${energy_body%%|*} == 200 ]]; grep -q 'energyKwh' <<<"${energy_body#*|}"
# Prove duplicate delivery is idempotent and an unavailable Influx write becomes a durable SQL recovery row.
payload='{"deviceId":2,"eventId":"wu4-duplicate","occurredAt":"2026-08-30T00:00:00Z","status":"online","reportedJson":"{\"power\":true}","energyKwh":1.5,"temperatureC":22.5,"voltageV":220.0}'
first_file="/tmp/iobuild-wu4-duplicate-a-$suffix"; duplicate_file="/tmp/iobuild-wu4-duplicate-b-$suffix"
status -H 'content-type: application/json' -d "$payload" "http://127.0.0.1:$api_port/api/v1/devices/telemetry" >"$first_file" & first_pid=$!
status -H 'content-type: application/json' -d "$payload" "http://127.0.0.1:$api_port/api/v1/devices/telemetry" >"$duplicate_file" & duplicate_pid=$!
wait "$first_pid"; wait "$duplicate_pid"; first=$(cat "$first_file"); duplicate=$(cat "$duplicate_file"); [[ ${first%%|*}:${duplicate%%|*} == 200:200 ]]; rm -f "$first_file" "$duplicate_file"
sudo -n -u hermes docker stop "$influx" >/dev/null
outage_payload='{"deviceId":2,"eventId":"wu4-outage","occurredAt":"2026-08-30T00:01:00Z","status":"online","reportedJson":"{}","energyKwh":2.5,"temperatureC":23.5,"voltageV":221.0}'
outage="$(status -H 'content-type: application/json' -d "$outage_payload" "http://127.0.0.1:$api_port/api/v1/devices/telemetry")"; [[ ${outage%%|*} == 200 ]]
recovery_before=$(sudo -n -u hermes docker exec "$mysql" mysql -N -uroot -piobuild iobuild -e "SELECT COUNT(*) FROM telemetry_recovery WHERE EventId='wu4-outage';" | tr -d '\r'); [[ "$recovery_before" == 1 ]]
sudo -n -u hermes docker start "$influx" >/dev/null; wait_http "http://127.0.0.1:$influx_port/health"
replay="$(status -X POST -H "$auth" "http://127.0.0.1:$api_port/api/v1/devices/telemetry/replay")"; [[ ${replay%%|*} == 200 ]]; python3 - "${replay#*|}" <<'PY3'
import json, sys
assert json.loads(sys.argv[1])["replayed"] >= 1
PY3
recovery_after=$(sudo -n -u hermes docker exec "$mysql" mysql -N -uroot -piobuild iobuild -e "SELECT COUNT(*) FROM telemetry_recovery WHERE EventId='wu4-outage';" | tr -d '\r'); [[ "$recovery_after" == 0 ]]
influx_rows=$(sudo -n -u hermes docker exec "$influx" influx query --org iobuild 'from(bucket:"telemetry") |> range(start: 2026-08-29T00:00:00Z) |> filter(fn: (r) => r._measurement == "telemetry") |> count()' --token wu4-token | grep -c telemetry || true); [[ "$influx_rows" -gt 0 ]]
# Prove a persisted command is republished after a real broker reconnect, together with registry state.
sudo -n -u hermes docker exec "$mysql" mysql -uroot -piobuild iobuild -e "INSERT INTO device_commands (DeviceId, CommandId, DesiredJson, IssuedAt, PublishAttempts) VALUES ($command_device_id, 'wu4-pending-reconnect', '{\"brightness\":42}', UTC_TIMESTAMP(6), 0);"
sudo -n -u hermes docker restart "$mosquitto" >/dev/null
sleep 2
for _ in $(seq 1 30); do pending_attempts=$(sudo -n -u hermes docker exec "$mysql" mysql -N -uroot -piobuild iobuild -e "SELECT PublishAttempts FROM device_commands WHERE CommandId='wu4-pending-reconnect';" | tr -d '\r'); [[ "$pending_attempts" -ge 1 ]] && break; sleep 1; done
[[ "$pending_attempts" -ge 1 ]]
registry_after_reconnect=$(retained_payload "registry/$command_device_id"); grep -q "\"deviceId\":$device_id" <<<"$registry_after_reconnect"
pending_retained=$(retained_payload "commands/$command_device_id"); grep -q '"brightness":42' <<<"$pending_retained"
kill "$api_pid"; wait "$api_pid" || true; api_pid=""; start_api
restart_status="$(status -H "$auth" "http://127.0.0.1:$api_port/api/v1/devices/$command_device_id/status")"; [[ ${restart_status%%|*} == 200 ]]
migrations=$(sudo -n -u hermes docker exec "$mysql" mysql -N -uroot -piobuild iobuild -e 'SELECT COUNT(*) FROM __EFMigrationsHistory;' | tr -d '\r'); [[ "$migrations" == 4 ]]
ack=$(sudo -n -u hermes docker exec "$mysql" mysql -N -uroot -piobuild iobuild -e "SELECT COUNT(*) FROM device_commands WHERE DeviceId=$command_device_id AND AcknowledgedAt IS NOT NULL;" | tr -d '\r'); [[ "$ack" -ge 1 ]]
telemetry_exact=$(sudo -n -u hermes docker exec "$mysql" mysql -N -uroot -piobuild iobuild -e "SELECT COUNT(*) FROM device_telemetry WHERE EventId='wu4-duplicate';" | tr -d '\r'); [[ "$telemetry_exact" == 1 ]]
printf 'PASS device=%s registry=%s command=%s telemetry=%s duplicate=%s recovery=%s/%s influx=%s ack=%s pending-reconnect=%s migrations=%s restart=%s resources=%s,%s,%s,%s,%s\n' "$command_device_id" "$registry" "$retained" "$telemetry_count" "$telemetry_exact" "$recovery_before" "$recovery_after" "$influx_rows" "$ack" "$pending_attempts" "$migrations" "${restart_status%%|*}" "$mysql" "$mosquitto" "$influx" "$simulator" "$network"
