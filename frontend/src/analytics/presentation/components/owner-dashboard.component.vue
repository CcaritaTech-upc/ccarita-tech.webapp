<script setup>
import { computed, ref, watch, onMounted, onActivated } from 'vue';
import { useI18n } from 'vue-i18n';
import { Line, Bar } from 'vue-chartjs';
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler } from 'chart.js';
import StatCard from './stat-card.component.vue';
import UnitCard from './unit-card.component.vue';
import LiveEnergyChart from './live-energy-chart.component.vue';
import { useAnalyticsStore } from '../../application/analytics.store.js';
import { useIamStore } from '../../../iam/application/iam.store.js';
import { ANALYTICS_DEFAULT_MINUTES } from '../../../shared/infrastructure/constants.js';
import { isOnlineStatus } from '../../../devices/domain/model/device-status.enum.js';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler);

const { t } = useI18n();

const props = defineProps({
  dashboard: {
    type: Object,
    required: true
  }
});

const analyticsStore = useAnalyticsStore();
const iamStore = useIamStore();

const userId = computed(() => iamStore.currentUser?.id ?? null);
const liveMinutes = ref(ANALYTICS_DEFAULT_MINUTES);

const timeRange = ref('24h');
const timeRangeOptions = computed(() => [
  { label: t('devices.telemetry.lastHour'), value: '1h' },
  { label: t('devices.telemetry.last24h'), value: '24h' }
]);

// BUG 1 fix: source selector exclusively from the owner-scoped dashboard payload.
// Do NOT use analyticsStore.devices (all-tenant list — builder dashboard dependency).
const deviceOptions = computed(() =>
  (props.dashboard?.deviceHealthStatus ?? []).map(d => ({ name: d.deviceName, id: d.deviceId }))
);

const selectedDevice = computed({
  get: () => analyticsStore.selectedDeviceId,
  set: (val) => {
    analyticsStore.selectDevice(typeof val === 'object' ? val?.id : val);
  }
});

const energyChartData = computed(() => {
  if (!analyticsStore.deviceEnergyReadings?.length) return null;

  const isHourly = timeRange.value === '24h';
  const bucketMs = isHourly ? 60 * 60 * 1000 : 5 * 60 * 1000;
  const bucketMap = new Map();

  analyticsStore.deviceEnergyReadings.forEach(r => {
    const ts = new Date(r.timestamp).getTime();
    const bucket = Math.floor(ts / bucketMs) * bucketMs;
    const existing = bucketMap.get(bucket) || { sum: 0, count: 0 };
    existing.sum += r.energyKwh;
    existing.count++;
    bucketMap.set(bucket, existing);
  });

  const sortedBuckets = [...bucketMap.keys()].sort();

  return {
    labels: sortedBuckets.map(b =>
      new Date(b).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })
    ),
    datasets: [{
      label: t('devices.telemetry.energyChart'),
      data: sortedBuckets.map(b => {
        const { sum, count } = bucketMap.get(b);
        return count > 0 ? parseFloat((sum / count).toFixed(2)) : 0;
      }),
      borderColor: '#8B5CF6',
      backgroundColor: 'rgba(139, 92, 246, 0.1)',
      fill: true,
      tension: 0.4
    }]
  };
});

const telemetryChartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      display: true,
      position: 'bottom'
    }
  },
  scales: {
    y: {
      beginAtZero: true,
      title: {
        display: true,
        text: 'kWh'
      }
    }
  }
};

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

// The health card uses the same wording as the device table: a standby device
// is reachable, but it is not reported as plain "online".
const healthLabel = (status) => {
  const s = String(status ?? '').trim().toLowerCase();
  if (s === 'idle' || s === 'standby') return t('devices.status.standby');
  return t(isOnlineStatus(status) ? 'analytics.owner.device.online' : 'analytics.owner.device.offline');
};

const formatLastSeen = (lastSeen) => {
  if (!lastSeen) return '—';
  return new Date(lastSeen).toLocaleString();
};

const formatDesiredValue = (val) => {
  if (val === null || val === undefined) return '—';
  if (typeof val === 'boolean') return t(val ? 'devices.control.on' : 'devices.control.off');
  if (typeof val === 'number') return val;
  const s = String(val);
  if (s.toLowerCase() === 'true') return t('devices.control.on');
  if (s.toLowerCase() === 'false') return t('devices.control.off');
  return s;
};

// Fetch telemetry when device or time range changes
const fetchTelemetry = async (deviceId) => {
  if (!deviceId) {
    analyticsStore.deviceEnergyReadings = [];
    analyticsStore.deviceStatus = null;
    return;
  }
  const now = new Date();
  const ms = timeRange.value === '1h' ? 60 * 60 * 1000 : 24 * 60 * 60 * 1000;
  const from = new Date(now.getTime() - ms);
  try {
    await Promise.all([
      analyticsStore.fetchDeviceEnergy(deviceId, from.toISOString(), now.toISOString()),
      analyticsStore.fetchDeviceStatus(deviceId)
    ]);
  } catch (error) {
    console.error('Error loading telemetry:', error);
  }
};

watch(() => analyticsStore.selectedDeviceId, async (newId) => {
  await fetchTelemetry(newId);
});

watch(timeRange, () => {
  if (analyticsStore.selectedDeviceId) {
    fetchTelemetry(analyticsStore.selectedDeviceId);
  }
});

// Auto-select first owned device when owner-scoped options change.
// Clears selection (null) when owner has no devices — prevents cross-tenant telemetry calls.
watch(() => deviceOptions.value, (opts) => {
  const ids = opts.map(o => o.id);
  if (!ids.includes(analyticsStore.selectedDeviceId)) {
    analyticsStore.selectDevice(opts[0]?.id ?? null);
  }
}, { immediate: true });

// Re-fetch device status every time this component mounts (navigation back from Device Management).
// The selectedDeviceId watcher doesn't fire if the id didn't change, so we need this guard.
const refetchStatusOnMount = () => {
  if (analyticsStore.selectedDeviceId) {
    fetchTelemetry(analyticsStore.selectedDeviceId);
  }
};
onMounted(refetchStatusOnMount);
onActivated(refetchStatusOnMount);

// Energy consumption chart (last 30 days)
const energyConsumptionChartData = computed(() => {
  if (!props.dashboard?.dailyEnergyConsumption?.length) return null;
  
  const data = props.dashboard.dailyEnergyConsumption.slice(-30);
  return {
    labels: data.map(point => new Date(point.timestamp).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })),
    datasets: [{
      label: t('analytics.owner.charts.dailyEnergyLabel'),
      data: data.map(point => point.value),
      borderColor: '#8B5CF6',
      backgroundColor: 'rgba(139, 92, 246, 0.1)',
      fill: true,
      tension: 0.4
    }]
  };
});

// Temperature comfort chart (last 7 days)
const temperatureComfortChartData = computed(() => {
  if (!props.dashboard?.temperatureHistory?.length) return null;
  
  const data = props.dashboard.temperatureHistory.slice(-7);
  return {
    labels: data.map(point => new Date(point.timestamp).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })),
    datasets: [{
      label: t('analytics.owner.charts.temperatureLabel'),
      data: data.map(point => point.value),
      borderColor: '#F59E0B',
      backgroundColor: 'rgba(245, 158, 11, 0.2)',
      fill: true,
      tension: 0.4
    }]
  };
});

// Water usage chart (last 7 days)
const waterUsageChartData = computed(() => {
  if (!props.dashboard?.waterUsageWeekly?.length) return null;
  
  return {
    labels: props.dashboard.waterUsageWeekly.map(point => 
      new Date(point.timestamp).toLocaleDateString('en-US', { weekday: 'short' })
    ),
    datasets: [{
      label: t('analytics.owner.charts.waterLabel'),
      data: props.dashboard.waterUsageWeekly.map(point => point.value),
      backgroundColor: '#06B6D4',
      borderRadius: 6
    }]
  };
});

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      display: true,
      position: 'bottom'
    }
  },
  scales: {
    y: {
      beginAtZero: true
    }
  }
};

</script>

<template>
  <div class="owner-dashboard">
    <div class="dashboard-header">
      <h2 class="dashboard-title">{{ $t('analytics.owner.title') }}</h2>
      <p class="dashboard-subtitle">{{ $t('analytics.owner.subtitle') }}</p>
    </div>
    
    <!-- Hero Stats -->
    <div class="stats-grid">
      <StatCard
        :title="$t('analytics.owner.stats.myUnits')"
        :value="dashboard.myUnitsCount"
        icon="pi-home"
        icon-bg-color="bg-purple-100"
        icon-text-color="text-purple-600"
      />
      <StatCard
        :title="$t('analytics.owner.stats.myDevices')"
        :value="dashboard.totalDevices"
        icon="pi-box"
        icon-bg-color="bg-blue-100"
        icon-text-color="text-blue-600"
        :subtitle="`${dashboard.onlineDevices} ${$t('analytics.owner.stats.online')}`"
      />
      <StatCard
        v-if="dashboard.energyThisMonth > 0"
        :title="$t('analytics.owner.stats.energyThisMonth')"
        :value="`${dashboard.energyThisMonth.toFixed(1)} kWh`"
        icon="pi-bolt"
        icon-bg-color="bg-yellow-100"
        icon-text-color="text-yellow-600"
      />
      <StatCard
        v-if="dashboard.temperatureAvg > 0"
        :title="$t('analytics.owner.stats.temperatureAvg')"
        :value="`${dashboard.temperatureAvg.toFixed(1)}°C`"
        icon="pi-thermometer"
        icon-bg-color="bg-orange-100"
        icon-text-color="text-orange-600"
      />
      <StatCard
        :title="$t('analytics.owner.stats.alerts')"
        :value="dashboard.alertsCount"
        icon="pi-bell"
        icon-bg-color="bg-red-100"
        icon-text-color="text-red-600"
      />
    </div>
    
    <!-- Charts Grid -->
    <div class="charts-grid">
      <div class="chart-container large">
        <h3 class="chart-title">{{ $t('analytics.owner.charts.energyConsumption') }}</h3>
        <div class="chart-wrapper">
          <Line v-if="energyConsumptionChartData" :data="energyConsumptionChartData" :options="chartOptions" />
          <p v-else class="no-data">{{ $t('analytics.owner.noData.energyConsumption') }}</p>
        </div>
      </div>
      
      <div class="chart-container">
        <h3 class="chart-title">{{ $t('analytics.owner.charts.temperatureComfort') }}</h3>
        <div class="chart-wrapper">
          <Line v-if="temperatureComfortChartData" :data="temperatureComfortChartData" :options="chartOptions" />
          <p v-else class="no-data">{{ $t('analytics.owner.noData.temperature') }}</p>
        </div>
      </div>
      
      <div class="chart-container">
        <h3 class="chart-title">{{ $t('analytics.owner.charts.waterUsage') }}</h3>
        <div class="chart-wrapper">
          <Bar v-if="waterUsageChartData" :data="waterUsageChartData" :options="chartOptions" />
          <p v-else class="no-data">{{ $t('analytics.owner.noData.waterUsage') }}</p>
        </div>
      </div>
    </div>
    
    <!-- Live Energy Chart -->
    <div class="chart-container live-energy-section">
      <h3 class="chart-title">{{ $t('analytics.live.title', { minutes: liveMinutes }) }}</h3>
      <p class="chart-subtitle">{{ $t('analytics.live.subtitle') }}</p>
      <div class="chart-wrapper">
        <LiveEnergyChart :user-id="userId" role="owner" :minutes="liveMinutes" />
      </div>
    </div>

    <!-- Device Telemetry Section -->
    <div class="section telemetry-section">
      <h3 class="section-title">{{ $t('devices.telemetry.selectDevice') }}</h3>
      <div class="telemetry-select-row">
        <pv-select
          v-if="deviceOptions.length"
          v-model="selectedDevice"
          :options="deviceOptions"
          option-label="name"
          option-value="id"
          class="telemetry-select"
          :placeholder="$t('devices.telemetry.selectDevice')"
        />
      </div>
      <div class="telemetry-time-range">
        <pv-select-button
          v-model="timeRange"
          :options="timeRangeOptions"
          option-label="label"
          option-value="value"
        />
      </div>

      <div v-if="analyticsStore.selectedDeviceId" class="telemetry-content">
        <!-- Energy Chart -->
        <div class="telemetry-chart-container">
          <h4 class="telemetry-chart-title">{{ $t('devices.telemetry.energyChart') }}</h4>
          <div class="chart-wrapper">
            <Line v-if="energyChartData" :data="energyChartData" :options="telemetryChartOptions" />
            <p v-else-if="analyticsStore.telemetryLoading" class="no-data">{{ $t('devices.telemetry.loading') }}</p>
            <p v-else class="no-data">{{ $t('devices.telemetry.noData') }}</p>
          </div>
        </div>

        <!-- Status Card -->
        <div class="telemetry-status-card">
          <h4 class="telemetry-chart-title">{{ $t('devices.telemetry.status') }}</h4>
          <div v-if="analyticsStore.deviceStatus" class="status-details">
            <div class="status-field">
              <span class="status-label">{{ $t('devices.telemetry.status') }}</span>
              <pv-tag
                :value="getStatusLabel(analyticsStore.deviceStatus.status)"
                :severity="getStatusSeverity(analyticsStore.deviceStatus.status)"
              />
            </div>
            <div class="status-field">
              <span class="status-label">{{ $t('devices.telemetry.lastSeen') }}</span>
              <span class="status-value">{{ formatLastSeen(analyticsStore.deviceStatus.lastSeen) }}</span>
            </div>
            <!-- Device-specific parameters from shadow (desired state) -->
            <template v-if="analyticsStore.deviceStatus.desired && Object.keys(analyticsStore.deviceStatus.desired).length">
              <div
                v-for="(val, key) in analyticsStore.deviceStatus.desired"
                :key="key"
                class="status-field"
              >
                <span class="status-label">{{ $te(`devices.control.attributes.${key}`) ? $t(`devices.control.attributes.${key}`) : key }}</span>
                <span class="status-value">{{ formatDesiredValue(val) }}</span>
              </div>
            </template>
            <!-- Fallback: ambient sensor readings when no desired state exists -->
            <template v-else>
              <div class="status-field">
                <span class="status-label">{{ $t('devices.telemetry.temperature') }}</span>
                <span class="status-value">{{ analyticsStore.deviceStatus.temperatureC != null ? `${analyticsStore.deviceStatus.temperatureC.toFixed(1)} °C` : '—' }}</span>
              </div>
              <div class="status-field">
                <span class="status-label">{{ $t('devices.telemetry.voltage') }}</span>
                <span class="status-value">{{ analyticsStore.deviceStatus.voltageV != null ? `${analyticsStore.deviceStatus.voltageV.toFixed(1)} V` : '—' }}</span>
              </div>
            </template>
            <!-- Voltage always shown -->
            <div v-if="analyticsStore.deviceStatus.desired && Object.keys(analyticsStore.deviceStatus.desired).length" class="status-field">
              <span class="status-label">{{ $t('devices.telemetry.voltage') }}</span>
              <span class="status-value">{{ analyticsStore.deviceStatus.voltageV != null ? `${analyticsStore.deviceStatus.voltageV.toFixed(1)} V` : '—' }}</span>
            </div>
          </div>
          <div v-else-if="analyticsStore.telemetryLoading" class="no-data">
            {{ $t('devices.telemetry.loading') }}
          </div>
          <div v-else class="no-data">
            {{ $t('devices.telemetry.noData') }}
          </div>
        </div>
      </div>
      <div v-else class="telemetry-empty">
        <p class="no-data">{{ $t('devices.telemetry.noData') }}</p>
      </div>
    </div>

    <!-- Device Health Status -->
    <div class="section">
      <h3 class="section-title">{{ $t('analytics.owner.sections.deviceHealth') }}</h3>
      <div class="device-health-grid">
        <div 
          v-for="device in dashboard.deviceHealthStatus" 
          :key="device.deviceId"
          class="device-health-card"
        >
          <div class="device-health-header">
            <div class="device-info">
              <i class="pi pi-box device-health-icon"></i>
              <span class="device-name">{{ device.deviceName }}</span>
            </div>
            <span
              class="device-status"
              :class="isOnlineStatus(device.status) ? 'status-online' : 'status-offline'"
            >
              {{ healthLabel(device.status) }}
            </span>
          </div>
          <div class="device-health-body">
            <p class="device-type">{{ device.type || device.deviceName || '—' }}</p>
          </div>
        </div>
      </div>
      <p v-if="!dashboard.deviceHealthStatus?.length" class="no-data">{{ $t('analytics.owner.noData.deviceHealth') }}</p>
    </div>
    
    <!-- My Units Overview -->
    <div class="section">
      <h3 class="section-title">{{ $t('analytics.owner.sections.myUnitsOverview') }}</h3>
      <div class="units-grid">
        <UnitCard
          v-for="unit in dashboard.myUnitsDetails"
          :key="unit.unitId"
          :unit="unit"
        />
      </div>
      <p v-if="!dashboard.myUnitsDetails?.length" class="no-data">{{ $t('analytics.owner.noData.units') }}</p>
    </div>
  </div>
</template>

<style scoped>
.owner-dashboard {
  padding: 1.5rem;
  max-width: 100%;
}

.dashboard-header {
  margin-bottom: 2rem;
}

.dashboard-title {
  font-size: 2rem;
  font-weight: 800;
  color: #111827;
  margin-bottom: 0.5rem;
}

.dashboard-subtitle {
  font-size: 1rem;
  color: #6B7280;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1.25rem;
  margin-bottom: 2rem;
}

.charts-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.chart-container {
  background: white;
  border-radius: 0.75rem;
  padding: 1.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.chart-container.large {
  grid-column: 1 / -1;
}

.chart-title {
  font-size: 1.125rem;
  font-weight: 700;
  color: #111827;
  margin-bottom: 0.25rem;
}

.chart-subtitle {
  font-size: 0.75rem;
  color: #6B7280;
  margin-bottom: 1rem;
}

.live-energy-section {
  margin-bottom: 1.5rem;
}

.chart-wrapper {
  height: 300px;
  position: relative;
}

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

.device-health-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1rem;
}

.device-health-card {
  background: #F9FAFB;
  border-radius: 0.75rem;
  padding: 1.25rem;
  border: 2px solid #F3F4F6;
  transition: all 0.2s;
}

.device-health-card:hover {
  border-color: #8B5CF6;
  box-shadow: 0 4px 12px rgba(139, 92, 246, 0.15);
}

.device-health-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.device-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.device-health-icon {
  color: #7C3AED;
}

.device-name {
  font-size: 0.875rem;
  font-weight: 700;
  color: #111827;
}

.device-status {
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
}

.status-online {
  background: #D1FAE5;
  color: #065F46;
}

.status-offline {
  background: #FEE2E2;
  color: #991B1B;
}

.device-health-body {
  margin-top: 0.75rem;
}

.progress-bar-container {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.progress-bar-bg {
  flex: 1;
  height: 0.75rem;
  background: #E5E7EB;
  border-radius: 9999px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  border-radius: 9999px;
  transition: width 0.3s ease;
}

.progress-label {
  font-size: 0.875rem;
  font-weight: 700;
  color: #111827;
  min-width: 3rem;
  text-align: right;
}

.device-type {
  font-size: 0.75rem;
  color: #6B7280;
  margin-top: 0.5rem;
}

.units-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.25rem;
}

.no-data {
  text-align: center;
  color: #9CA3AF;
  padding: 1rem;
  font-size: 0.8125rem;
}

/* Chart wrappers have fixed height; shrink the no-data placeholder so it doesn't
   dominate the space when telemetry is intentionally empty (ADR-6). */
.chart-wrapper .no-data {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  padding: 0;
  margin: 0;
  white-space: nowrap;
}

/* Telemetry Section */
.telemetry-section .telemetry-select-row {
  margin-bottom: 1.5rem;
}

.telemetry-select {
  min-width: 280px;
  max-width: 400px;
}

.telemetry-select .p-select-label,
.telemetry-select .p-select-value {
  color: #111827 !important;
}

.telemetry-time-range {
  margin-bottom: 1rem;
}

.telemetry-select .p-select-option {
  color: #111827 !important;
}

.telemetry-content {
  display: grid;
  grid-template-columns: 1fr 320px;
  gap: 1.5rem;
}

.telemetry-chart-container {
  background: #F9FAFB;
  border-radius: 0.75rem;
  padding: 1.25rem;
  border: 2px solid #F3F4F6;
}

.telemetry-chart-title {
  font-size: 0.875rem;
  font-weight: 700;
  color: #111827;
  margin-bottom: 1rem;
}

.telemetry-status-card {
  background: #F9FAFB;
  border-radius: 0.75rem;
  padding: 1.25rem;
  border: 2px solid #F3F4F6;
}

.status-details {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.status-field {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0;
  border-bottom: 1px solid #F3F4F6;
}

.status-field:last-child {
  border-bottom: none;
}

.status-label {
  font-size: 0.8125rem;
  font-weight: 600;
  color: #6B7280;
}

.status-value {
  font-size: 0.875rem;
  font-weight: 600;
  color: #111827;
}

.telemetry-empty {
  padding: 1rem 0;
}

@media (max-width: 1024px) {
  .telemetry-content {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 768px) {
  .owner-dashboard {
    padding: 1rem;
  }
  
  .charts-grid {
    grid-template-columns: 1fr;
  }
  
  .chart-container.large {
    grid-column: 1;
  }
  
  .device-health-grid,
  .units-grid {
    grid-template-columns: 1fr;
  }
  
  .telemetry-select {
    min-width: 100%;
  }
}
</style>
