<script setup>
import { computed, ref, watch, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { Line, Bar, Doughnut } from 'vue-chartjs';
import { Chart as ChartJS, CategoryScale, LinearScale, PointElement, LineElement, BarElement, ArcElement, Title, Tooltip, Legend, Filler } from 'chart.js';
import StatCard from './stat-card.component.vue';
import ProjectCard from './project-card.component.vue';
import LiveEnergyChart from './live-energy-chart.component.vue';
import { useAnalyticsStore } from '../../application/analytics.store.js';
import { useIamStore } from '../../../iam/application/iam.store.js';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, BarElement, ArcElement, Title, Tooltip, Legend, Filler);

const { t, te } = useI18n();

const props = defineProps({
  dashboard: {
    type: Object,
    required: true
  }
});

const analyticsStore = useAnalyticsStore();
const iamStore = useIamStore();

const userId = computed(() => iamStore.currentUser?.id ?? null);
const liveMinutes = ref(10);

const timeRange = ref('24h'); // '1h' or '24h'

const timeRangeOptions = [
  { label: t('devices.telemetry.lastHour'), value: '1h' },
  { label: t('devices.telemetry.last24h'), value: '24h' }
];

// Filter devices to those belonging to this builder's projects only.
// Without this guard, getAllDevices() returns devices from ALL builders.
const builderProjectIds = computed(() =>
  (props.dashboard?.projectsOverview ?? []).map(p => Number(p.id))
);

const deviceOptions = computed(() =>
  analyticsStore.devices
    .filter(d => builderProjectIds.value.includes(Number(d.projectId)))
    .map(d => ({ name: d.name, id: d.id }))
);

// Keep selectedDeviceId in sync: clear stale selection when the device list changes
// (e.g. switching between builders, or when this builder has no devices yet).
watch(deviceOptions, (opts) => {
  const ids = opts.map(o => o.id);
  if (!ids.includes(analyticsStore.selectedDeviceId)) {
    analyticsStore.selectDevice(opts[0]?.id ?? null);
  }
}, { immediate: true });

const selectedDevice = computed({
  get: () => analyticsStore.selectedDeviceId,
  set: (val) => {
    analyticsStore.selectDevice(typeof val === 'object' ? val?.id : val);
  }
});

const energyChartData = computed(() => {
  if (!analyticsStore.deviceEnergyReadings?.length) return null;

  const isHourly = timeRange.value === '24h';
  const timeFormat = isHourly
    ? { hour: '2-digit', minute: '2-digit' }
    : { hour: '2-digit', minute: '2-digit', second: '2-digit' };

  // Aggregate readings — by hour for 24h, by 5min for 1h
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
    labels: sortedBuckets.map(b => new Date(b).toLocaleTimeString('en-US', isHourly
      ? { hour: '2-digit', minute: '2-digit' }
      : { hour: '2-digit', minute: '2-digit' }
    )),
    datasets: [{
      label: t('devices.telemetry.energyChart'),
      data: sortedBuckets.map(b => {
        const { sum, count } = bucketMap.get(b);
        return count > 0 ? parseFloat((sum / count).toFixed(2)) : 0;
      }),
      borderColor: '#3B82F6',
      backgroundColor: 'rgba(59, 130, 246, 0.1)',
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
  if (status === 'Online') return 'success';
  if (status === 'Offline') return 'danger';
  return 'info';
};

const getStatusLabel = (status) => {
  if (status === 'Online') return t('devices.status.online');
  if (status === 'Offline') return t('devices.status.offline');
  return status || t('devices.telemetry.unknown');
};

const formatLastSeen = (lastSeen) => {
  if (!lastSeen) return '—';
  return new Date(lastSeen).toLocaleString();
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

onMounted(() => {
  analyticsStore.fetchDevices();
});

// Chart configurations
const hourlyEnergyChartData = computed(() => {
  if (!props.dashboard?.hourlyEnergyData?.length) return null;
  
  const data = props.dashboard.hourlyEnergyData.slice(-24);
  return {
    labels: data.map(point => new Date(point.timestamp).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })),
    datasets: [{
      label: t('analytics.builder.charts.energyLabel'),
      data: data.map(point => point.value),
      borderColor: '#3B82F6',
      backgroundColor: 'rgba(59, 130, 246, 0.1)',
      fill: true,
      tension: 0.4
    }]
  };
});

const monthlyOccupancyChartData = computed(() => {
  if (!props.dashboard?.monthlyOccupancy?.length) return null;

  return {
    labels: props.dashboard.monthlyOccupancy.map(m => {
      const d = new Date(m.timestamp || m.month);
      return d.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
    }),
    datasets: [{
      label: t('analytics.builder.charts.occupancyLabel'),
      data: props.dashboard.monthlyOccupancy.map(m => m.value ?? m.occupancyRate),
      backgroundColor: '#10B981',
      borderRadius: 6
    }]
  };
});

const temperatureTrendChartData = computed(() => {
  if (!props.dashboard?.temperatureHistory?.length) return null;
  
  const data = props.dashboard.temperatureHistory.slice(-7);
  return {
    labels: data.map(point => new Date(point.timestamp).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })),
    datasets: [{
      label: t('analytics.builder.charts.temperatureLabel'),
      data: data.map(point => point.value),
      borderColor: '#F59E0B',
      backgroundColor: 'rgba(245, 158, 11, 0.2)',
      fill: true,
      tension: 0.4
    }]
  };
});

const deviceTypeChartData = computed(() => {
  if (!props.dashboard?.devicesByType || Object.keys(props.dashboard.devicesByType).length === 0) return null;
  
  const colors = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6', '#EC4899', '#14B8A6'];
  return {
    labels: Object.keys(props.dashboard.devicesByType).map(type => translateDeviceType(type)),
    datasets: [{
      data: Object.values(props.dashboard.devicesByType),
      backgroundColor: colors.slice(0, Object.keys(props.dashboard.devicesByType).length),
      borderWidth: 2,
      borderColor: '#fff'
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

const doughnutOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'right'
    }
  }
};

// Device type icons
const deviceTypeIcons = {
  'SmartMeter': 'pi-bolt',
  'smartmeter': 'pi-bolt',
  'WaterSensor': 'pi-wave-pulse',
  'watersensor': 'pi-wave-pulse',
  'SmokeDetector': 'pi-shield',
  'smokedetector': 'pi-shield',
  'AirConditioner': 'pi-sync',
  'airconditioner': 'pi-sync',
  'SmartLight': 'pi-lightbulb',
  'smartlight': 'pi-lightbulb',
  'Temperature': 'pi-chart-line',
  'temperature': 'pi-chart-line',
  'Humidity': 'pi-cloud',
  'humidity': 'pi-cloud',
  'Energy': 'pi-bolt',
  'energy': 'pi-bolt',
  'Water': 'pi-wave-pulse',
  'water': 'pi-wave-pulse',
  'Security': 'pi-shield',
  'security': 'pi-shield',
  'Construction': 'pi-wrench',
  'construction': 'pi-wrench',
  'Access Control': 'pi-lock',
  'access control': 'pi-lock',
  'HVAC': 'pi-sync',
  'hvac': 'pi-sync',
  'Lighting': 'pi-lightbulb',
  'lighting': 'pi-lightbulb'
};

const getDeviceIcon = (type) => {
  if (!type) return 'pi-box';
  return deviceTypeIcons[type] || deviceTypeIcons[type.toLowerCase()] || 'pi-box';
};

const translateDeviceType = (type) => {
  if (!type) return '';
  const key = `analytics.deviceTypes.${type}`;
  if (te(key)) return t(key);
  const catalogKey = `devices.deviceCatalog.${type}`;
  if (te(catalogKey)) return t(catalogKey);
  return t(key, type);
};
</script>

<template>
  <div class="builder-dashboard">
    <div class="dashboard-header">
      <h2 class="dashboard-title">{{ $t('analytics.builder.title') }}</h2>
      <p class="dashboard-subtitle">{{ $t('analytics.builder.subtitle') }}</p>
    </div>
    
    <!-- Hero Stats -->
    <div class="stats-grid">
      <StatCard
        :title="$t('analytics.builder.stats.activeProjects')"
        :value="dashboard.activeProjectsCount"
        icon="pi-briefcase"
        icon-bg-color="bg-blue-100"
        icon-text-color="text-blue-600"
      />
      <StatCard
        :title="$t('analytics.builder.stats.connectedDevices')"
        :value="dashboard.totalDevices"
        icon="pi-box"
        icon-bg-color="bg-green-100"
        icon-text-color="text-green-600"
        :subtitle="`${dashboard.onlineDevices} ${$t('analytics.builder.stats.online')}`"
      />
      <StatCard
        :title="$t('analytics.builder.stats.occupiedUnits')"
        :value="`${dashboard.occupiedUnits ?? 0}/${dashboard.totalUnits ?? 0}`"
        icon="pi-building"
        icon-bg-color="bg-purple-100"
        icon-text-color="text-purple-600"
        :subtitle="`${(dashboard.occupancyRate ?? 0).toFixed(1)}% ${$t('analytics.builder.stats.occupancy')}`"
      />
      <StatCard
        :title="$t('analytics.builder.stats.totalUnits')"
        :value="dashboard.totalUnits ?? 0"
        icon="pi-home"
        icon-bg-color="bg-orange-100"
        icon-text-color="text-orange-600"
      />
      <StatCard
        :title="$t('analytics.builder.stats.alerts')"
        :value="dashboard.alertsCount ?? 0"
        icon="pi-exclamation-triangle"
        icon-bg-color="bg-red-100"
        icon-text-color="text-red-600"
      />
      <StatCard
        :title="$t('analytics.builder.stats.energyEfficiency')"
        :value="`${(dashboard.energyEfficiencyAvg ?? 0).toFixed(1)} kWh`"
        icon="pi-chart-line"
        icon-bg-color="bg-teal-100"
        icon-text-color="text-teal-600"
        :subtitle="$t('analytics.builder.stats.averageConsumption')"
      />
    </div>
    
    <!-- Charts Grid -->
    <div class="charts-grid">
      <div class="chart-container">
        <h3 class="chart-title">{{ $t('analytics.builder.charts.hourlyEnergy') }}</h3>
        <div class="chart-wrapper">
          <Line v-if="hourlyEnergyChartData" :data="hourlyEnergyChartData" :options="chartOptions" />
          <p v-else class="no-data">{{ $t('analytics.builder.noData.hourlyEnergy') }}</p>
        </div>
      </div>
      
      <div class="chart-container">
        <h3 class="chart-title">{{ $t('analytics.builder.charts.monthlyOccupancy') }}</h3>
        <div class="chart-wrapper">
          <Bar v-if="monthlyOccupancyChartData" :data="monthlyOccupancyChartData" :options="chartOptions" />
          <p v-else class="no-data">{{ $t('analytics.builder.noData.occupancy') }}</p>
        </div>
      </div>
      
      <div class="chart-container">
        <h3 class="chart-title">{{ $t('analytics.builder.charts.temperatureTrends') }}</h3>
        <div class="chart-wrapper">
          <Line v-if="temperatureTrendChartData" :data="temperatureTrendChartData" :options="chartOptions" />
          <p v-else class="no-data">{{ $t('analytics.builder.noData.temperature') }}</p>
        </div>
      </div>
      
      <div class="chart-container">
        <h3 class="chart-title">{{ $t('analytics.builder.charts.deviceDistribution') }}</h3>
        <div class="chart-wrapper">
          <Doughnut v-if="deviceTypeChartData" :data="deviceTypeChartData" :options="doughnutOptions" />
          <p v-else class="no-data">{{ $t('analytics.builder.noData.device') }}</p>
        </div>
      </div>
    </div>
    
    <!-- Live Energy Chart -->
    <div class="chart-container live-energy-section">
      <h3 class="chart-title">Live Energy — Last {{ liveMinutes }} min</h3>
      <p class="chart-subtitle">Auto-refreshes every 30s</p>
      <div class="chart-wrapper">
        <LiveEnergyChart v-if="userId" :user-id="userId" role="builder" :minutes="liveMinutes" />
      </div>
    </div>

    <!-- Device Telemetry Section -->
    <div class="section telemetry-section">
      <h3 class="section-title">{{ $t('devices.telemetry.selectDevice') }}</h3>
      <div class="telemetry-select-row">
        <pv-select
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
            <div class="status-field">
              <span class="status-label">{{ $t('devices.telemetry.temperature') }}</span>
              <span class="status-value">{{ analyticsStore.deviceStatus.temperatureC != null ? `${analyticsStore.deviceStatus.temperatureC.toFixed(1)} °C` : '—' }}</span>
            </div>
            <div class="status-field">
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

    <!-- Devices Distribution -->
    <div class="section">
      <h3 class="section-title">{{ $t('analytics.builder.sections.devicesDistribution') }}</h3>
      <div class="devices-grid">
        <div 
          v-for="(count, type) in dashboard.devicesByType" 
          :key="type"
          class="device-type-card"
        >
          <div class="device-type-icon">
            <i :class="['pi', getDeviceIcon(type), 'text-2xl']"></i>
          </div>
          <div class="device-type-info">
            <p class="device-type-name">{{ translateDeviceType(type) }}</p>
            <p class="device-type-count">{{ count }} {{ $t('analytics.builder.sections.devices') }}</p>
          </div>
        </div>
      </div>
    </div>
    
    <!-- Projects Overview -->
    <div class="section">
      <h3 class="section-title">{{ $t('analytics.builder.sections.projectsOverview') }}</h3>
      <div class="projects-grid">
        <ProjectCard
          v-for="project in dashboard.projectsOverview"
          :key="project.id"
          :project="project"
        />
      </div>
      <p v-if="!dashboard.projectsOverview?.length" class="no-data">{{ $t('analytics.builder.noData.projects') }}</p>
    </div>
  </div>
</template>

<style scoped>
.builder-dashboard {
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
  grid-template-columns: repeat(auto-fit, minmax(450px, 1fr));
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.chart-container {
  background: white;
  border-radius: 0.75rem;
  padding: 1.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
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

.devices-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1rem;
}

.device-type-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background: #F9FAFB;
  border-radius: 0.5rem;
  transition: all 0.2s;
}

.device-type-card:hover {
  background: #F3F4F6;
  transform: translateY(-2px);
}

.device-type-icon {
  width: 3rem;
  height: 3rem;
  border-radius: 0.5rem;
  background: white;
  color: #3B82F6;
  display: flex;
  align-items: center;
  justify-content: center;
}

.device-type-info {
  flex: 1;
}

.device-type-name {
  font-size: 0.875rem;
  font-weight: 600;
  color: #111827;
  margin-bottom: 0.125rem;
}

.device-type-count {
  font-size: 0.75rem;
  color: #6B7280;
}

.projects-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
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

.telemetry-time-range {
  margin-bottom: 1rem;
}

.telemetry-time-range .p-selectbutton {
  --p-selectbutton-color: #111827;
}

.telemetry-select .p-select-label,
.telemetry-select .p-select-value {
  color: #111827 !important;
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
  .builder-dashboard {
    padding: 1rem;
  }
  
  .charts-grid {
    grid-template-columns: 1fr;
  }
  
  .projects-grid {
    grid-template-columns: 1fr;
  }
  
  .devices-grid {
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  }

  .telemetry-select {
    min-width: 100%;
  }
}
</style>
