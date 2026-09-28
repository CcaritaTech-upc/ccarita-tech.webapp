<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import { useCommandStore } from '../../application/command.store.js';
import { useAnalyticsStore } from '../../../analytics/application/analytics.store.js';
import { useDeviceStore } from '../../application/device.store.js';
import { DeviceApi } from '../../infrastructure/device-api.js';
import { parsePower, isControlLocked } from '../../application/control-state.js';
import { useToast } from 'primevue/usetoast';
import { TOAST_SUCCESS_DURATION_MS, TOAST_AUTH_ERROR_DURATION_MS } from '../../../shared/infrastructure/constants.js';

const emit = defineEmits(['status-changed']);

const props = defineProps({
  /**
   * Device row from the unit-devices table.
   * Expected shape: { id, deviceName, type }
   * where `type` matches a DeviceCapabilityCatalog code (e.g. 'AirConditioner').
   */
  device: {
    type: Object,
    required: true,
  },
  /**
   * ControllableAttributes array for this device type.
   * Shape: [{ name, type, min, max, unit, enumMembers }]
   * `type` is the catalog value kind: 'number', 'enum', 'boolean'
   * (comparison is case-insensitive to stay resilient to contract casing).
   */
  controllableAttributes: {
    type: Array,
    default: () => [],
  },
});

const commandStore = useCommandStore();
const analyticsStore = useAnalyticsStore();
const deviceStore = useDeviceStore();
const toast = useToast();
const deviceApi = new DeviceApi();

// Fixed dropdown options for boolean attributes — the catalog does not send enumMembers for them.
const BOOLEAN_OPTIONS = [
  { label: 'On', value: true },
  { label: 'Off', value: false },
];

// Normalise the attribute value kind so comparisons are casing-agnostic.
function kind(attr) {
  return (attr?.type ?? '').toLowerCase();
}

// Local reactive values — one entry per controllable attribute
const localValues = ref({});
// Desired state fetched from the device shadow (null until loaded)
const shadowDesired = ref(null);
// Committed power state (true/false, null = unknown). Follows the shadow and
// successful power commands, not the unsent dropdown selection.
const committedPower = ref(null);

function locked(attr) {
  return isControlLocked(attr.name, committedPower.value);
}

function initFromAttrs(attrs, desired) {
  const init = {};
  attrs.forEach((attr) => {
    // Prefer the shadow's desired value; fall back to sensible defaults.
    if (desired && Object.prototype.hasOwnProperty.call(desired, attr.name)) {
      const raw = desired[attr.name];
      const k = kind(attr);
      if (k === 'number') {
        init[attr.name] = Number(raw);
      } else if (k === 'boolean') {
        init[attr.name] = raw === true || raw === 'true' || raw === 'On' || raw === 'on';
      } else {
        init[attr.name] = raw;
      }
    } else {
      const k = kind(attr);
      if (k === 'number') {
        init[attr.name] = attr.min ?? 0;
      } else if (k === 'boolean') {
        init[attr.name] = false;
      } else if (k === 'enum') {
        init[attr.name] = attr.enumMembers?.[0] ?? '';
      } else {
        init[attr.name] = '';
      }
    }
  });
  localValues.value = init;
}

// Re-initialise whenever attributes change (device change or catalog load)
watch(
  () => props.controllableAttributes,
  (attrs) => initFromAttrs(attrs, shadowDesired.value),
  { immediate: true }
);

// Fetch the shadow desired state from the backend on mount, then re-seed localValues.
onMounted(async () => {
  const id = deviceId.value;
  if (!id) return;
  try {
    const status = await deviceApi.getDeviceStatus(id);
    if (status?.desired && Object.keys(status.desired).length) {
      shadowDesired.value = status.desired;
      committedPower.value = parsePower(status.desired.power);
      initFromAttrs(props.controllableAttributes, status.desired);
    }
  } catch {
    // Shadow not available — keep defaults
  }
});

const hasControls = computed(() => props.controllableAttributes.length > 0);

// The device row may come from the analytics dashboard payload (deviceId) or the
// devices service (id). Accept either so the command targets the right device.
const deviceId = computed(() => props.device?.deviceId ?? props.device?.id);

function enumOptions(attr) {
  if (kind(attr) === 'boolean') return BOOLEAN_OPTIONS;
  return (attr.enumMembers ?? []).map((m) => ({ label: m, value: m }));
}

async function onSend(attr) {
  if (locked(attr)) return;
  const value = localValues.value[attr.name];
  const result = await commandStore.sendCommand(deviceId.value, attr.name, value);

  if (result.success) {
    toast.add({
      severity: 'success',
      summary: 'Command sent',
      detail: `${attr.name} set to ${value}${attr.unit ? ' ' + attr.unit : ''}`,
      life: TOAST_SUCCESS_DURATION_MS,
    });

    if (attr.name === 'power') {
      const isPowerOn = parsePower(value) === true;
      committedPower.value = isPowerOn;
      const newStatus = isPowerOn ? 'online' : 'idle';

      // 1. Mutación directa e inmediata del objeto de la fila en la tabla
      if (props.device) {
        props.device.status = newStatus;
      }

      // 2. Actualizar ownerDashboard en analyticsStore si existe
      if (analyticsStore.ownerDashboard?.deviceHealthStatus) {
        const found = analyticsStore.ownerDashboard.deviceHealthStatus.find(
          (d) => Number(d.deviceId ?? d.id) === Number(deviceId.value)
        );
        if (found) {
          found.status = newStatus;
        }
      }

      // 3. Actualizar deviceStore si existe
      if (deviceStore.devices) {
        const foundDev = deviceStore.devices.find(
          (d) => Number(d.id) === Number(deviceId.value)
        );
        if (foundDev) {
          foundDev.status = newStatus;
        }
      }

      // 4. Actualizar deviceStatus si está seleccionado en el Dashboard
      if (analyticsStore.deviceStatus && Number(analyticsStore.selectedDeviceId) === Number(deviceId.value)) {
        analyticsStore.deviceStatus.status = newStatus;
        if (!analyticsStore.deviceStatus.desired) {
          analyticsStore.deviceStatus.desired = {};
        }
        analyticsStore.deviceStatus.desired.power = isPowerOn;
      }

      emit('status-changed', { deviceId: deviceId.value, status: newStatus });
    }

    // 5. Sincronización en segundo plano: re-consultar estado confirmado tras respuesta del simulador
    setTimeout(async () => {
      try {
        const fresh = await deviceApi.getDeviceStatus(deviceId.value);
        if (fresh?.desired && 'power' in fresh.desired) {
          committedPower.value = parsePower(fresh.desired.power);
        }
        if (fresh?.status) {
          if (props.device) {
            props.device.status = fresh.status;
          }
          if (analyticsStore.ownerDashboard?.deviceHealthStatus) {
            const found = analyticsStore.ownerDashboard.deviceHealthStatus.find(
              (d) => Number(d.deviceId ?? d.id) === Number(deviceId.value)
            );
            if (found) {
              found.status = fresh.status;
            }
          }
          if (analyticsStore.deviceStatus && Number(analyticsStore.selectedDeviceId) === Number(deviceId.value)) {
            analyticsStore.deviceStatus = fresh;
          }
        }
      } catch {
        // Ignorar en sincronización pasiva
      }
    }, 1500);
  } else {
    toast.add({
      severity: result.status === 403 ? 'warn' : 'error',
      summary: result.status === 403 ? 'Permission denied' : 'Command failed',
      detail: result.message,
      life: TOAST_AUTH_ERROR_DURATION_MS,
    });
  }
}
</script>

<template>
  <div v-if="hasControls" class="control-panel">
    <div
      v-for="attr in controllableAttributes"
      :key="attr.name"
      class="control-row"
      :class="{ 'control-row--locked': locked(attr) }"
    >
      <div class="control-meta">
        <span class="control-label">{{ attr.name }}</span>
        <span v-if="attr.unit" class="control-unit">{{ attr.unit }}</span>
      </div>

      <!-- Numeric range: use native range input (no Slider registered in this project) -->
      <template v-if="kind(attr) === 'number'">
        <div class="slider-group">
          <input
            type="range"
            :min="attr.min"
            :max="attr.max"
            :step="1"
            v-model.number="localValues[attr.name]"
            :disabled="locked(attr)"
            class="native-slider"
          />
          <span class="slider-value">{{ localValues[attr.name] }}</span>
        </div>
      </template>

      <!-- Enum / Boolean: dropdown -->
      <template v-else-if="kind(attr) === 'enum' || kind(attr) === 'boolean'">
        <pv-select
          v-model="localValues[attr.name]"
          :options="enumOptions(attr)"
          option-label="label"
          option-value="value"
          :disabled="locked(attr)"
          class="control-select"
        />
      </template>

      <pv-button
        label="Send"
        icon="pi pi-send"
        size="small"
        :loading="commandStore.sending"
        :disabled="locked(attr)"
        @click="onSend(attr)"
        class="send-btn"
      />
    </div>
  </div>
</template>

<style scoped>
.control-panel {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 0.75rem 0;
}

.control-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
  padding: 0.25rem 0;
}

.control-row--locked {
  opacity: 0.5;
}

.control-row--locked .native-slider {
  cursor: not-allowed;
}

.control-meta {
  display: flex;
  flex-direction: column;
  min-width: 160px;
  max-width: 220px;
  flex-shrink: 0;
}

.control-label {
  font-size: 0.8125rem;
  font-weight: 600;
  color: #374151;
  white-space: normal;
  word-break: break-word;
  line-height: 1.3;
}

.control-unit {
  font-size: 0.75rem;
  color: #9CA3AF;
}

.slider-group {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
}

.native-slider {
  flex: 1;
  accent-color: #8B5CF6;
  cursor: pointer;
}

.slider-value {
  min-width: 2.5rem;
  text-align: right;
  font-size: 0.875rem;
  font-weight: 700;
  color: #111827;
}

.control-select {
  flex: 1;
  min-width: 140px;
}

.send-btn {
  flex-shrink: 0;
}
</style>
