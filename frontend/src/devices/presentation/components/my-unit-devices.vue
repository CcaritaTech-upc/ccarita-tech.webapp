<script setup>
import { computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import DeviceControlPanel from './device-control-panel.vue';
import { useAnalyticsStore } from '../../../analytics/application/analytics.store.js';
import { useDeviceStore } from '../../application/device.store.js';
import { useIamStore } from '../../../iam/application/iam.store.js';
import { isOnlineStatus } from '../../domain/model/device-status.enum.js';

const { t } = useI18n();

const analyticsStore = useAnalyticsStore();
const deviceStore = useDeviceStore();
const iamStore = useIamStore();

// Role guard: control affordances are Owner-only.
const isOwner = computed(() => (iamStore.currentUser?.role ?? '').toLowerCase() === 'owner');
const userId = computed(() => iamStore.currentUser?.id ?? null);

// Owner-scoped device list — sourced exclusively from the owner analytics payload
// (deviceHealthStatus), never from the all-tenant device store.
const devices = computed(() => analyticsStore.ownerDashboard?.deviceHealthStatus ?? []);

/**
 * Map deviceType code → controllable attributes array, sourced from the
 * global catalog (deviceStore.deviceTypes, loaded from GET /types).
 */
const controllableAttributesByType = computed(() => {
  const map = {};
  (deviceStore.deviceTypes ?? []).forEach((dt) => {
    map[dt.code] = dt.controllableAttributes ?? [];
  });
  return map;
});

function getControllableAttrs(deviceType) {
  return controllableAttributesByType.value[deviceType] ?? [];
}

const getStatusSeverity = (status) => {
  const s = String(status ?? '').trim().toLowerCase();
  if (s === 'online' || s === 'active') return 'success';
  if (s === 'idle' || s === 'standby') return 'warn';
  if (status) return 'danger';
  return 'info'; // unknown / empty
};

const getStatusLabel = (status) => {
  const s = String(status ?? '').trim().toLowerCase();
  if (s === 'online' || s === 'active') return t('devices.status.online');
  if (s === 'idle' || s === 'standby') return t('devices.status.standby');
  if (status) return t('devices.status.offline');
  return t('devices.telemetry.unknown');
};

const formatLastSeen = (lastSeen) => {
  if (!lastSeen) return '—';
  return new Date(lastSeen).toLocaleString();
};

onMounted(() => {
  deviceStore.loadDeviceTypes();
  // Ensure the owner-scoped payload is available even when the analytics dashboard
  // view was never visited this session.
  if (isOwner.value && userId.value != null && !analyticsStore.ownerDashboard) {
    analyticsStore.fetchOwnerDashboard(userId.value);
  }
});
</script>

<template>
  <div class="section">
    <h3 class="section-title">{{ t('devices.myUnit.title') }}</h3>
    <pv-data-table
      v-if="devices.length"
      :value="devices"
      class="unit-devices-table"
      striped-rows
      size="small"
    >
      <pv-column field="deviceName" :header="t('devices.myUnit.name')" />
      <pv-column :header="t('devices.myUnit.status')">
        <template #body="{ data }">
          <pv-tag
            :value="getStatusLabel(data.status)"
            :severity="getStatusSeverity(data.status)"
          />
        </template>
      </pv-column>
      <pv-column :header="t('devices.myUnit.lastOnline')">
        <template #body="{ data }">
          <span>{{ formatLastSeen(data.lastOnline) }}</span>
        </template>
      </pv-column>
      <!-- Control column — visible only for Owner role -->
      <pv-column v-if="isOwner" :header="t('devices.myUnit.control')">
        <template #body="{ data }">
          <DeviceControlPanel
            :device="data"
            :controllable-attributes="getControllableAttrs(data.type)"
          />
          <span
            v-if="getControllableAttrs(data.type).length === 0"
            class="no-controls-label"
          >
            —
          </span>
        </template>
      </pv-column>
    </pv-data-table>
    <p v-else class="no-data">{{ t('devices.myUnit.empty') }}</p>
  </div>
</template>

<style scoped>
.section {
  background: white;
  border-radius: 0.75rem;
  padding: 1.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  margin-bottom: 1.5rem;
}

.section-title {
  font-size: 1.25rem;
  font-weight: 700;
  color: #111827;
  margin-bottom: 1.5rem;
}

.no-data {
  text-align: center;
  color: #9CA3AF;
  padding: 2rem;
  font-size: 0.875rem;
}

.no-controls-label {
  color: #9CA3AF;
  font-size: 0.875rem;
}
</style>
