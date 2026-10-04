import { defineStore } from "pinia";
import { ref } from "vue";
import { AnalyticsApi } from "../infrastructure/analytics-api.js";
import { DeviceApi } from "@/devices/infrastructure/device-api.js";
import { ProjectApi } from "@/projects/infrastructure/project-api.js";
import { ANALYTICS_DEFAULT_MINUTES, POLLING_INTERVAL_MS } from "@/shared/infrastructure/constants.js";

const analyticsApi = new AnalyticsApi();
const deviceApi = new DeviceApi();
const projectApi = new ProjectApi();

export const useAnalyticsStore = defineStore("analytics", () => {
    const builderDashboard = ref(null);
    const ownerDashboard = ref(null);
    const historicalData = ref([]);
    const loading = ref(false);
    const errors = ref([]);

    const deviceEnergyReadings = ref([]);
    const deviceStatus = ref(null);
    const selectedDeviceId = ref(null);
    const devices = ref([]);
    const telemetryLoading = ref(false);

    const liveEnergyData = ref([]);
    const liveEnergyLoading = ref(false);
    let _liveEnergyInterval = null;

    async function fetchBuilderDashboard(builderId, silent = false) {
        if (!silent) loading.value = true;
        try {
            const rawDashboard = await analyticsApi.getBuilderDashboard(builderId);

            // Reconcile with active projects to purge any ghost/deleted projects from metrics
            if (rawDashboard && Array.isArray(rawDashboard.projectsOverview)) {
                try {
                    const projectsRes = await projectApi.getProjectsByBuilderId(builderId);
                    const activeList = projectsRes?.data || [];
                    const activeIds = new Set(activeList.map(p => Number(p.id)));

                    rawDashboard.projectsOverview = rawDashboard.projectsOverview.filter(p => activeIds.has(Number(p.id)));
                    rawDashboard.activeProjectsCount = activeList.length;
                    rawDashboard.totalDevices = rawDashboard.projectsOverview.reduce((sum, p) => sum + (Number(p.deviceCount) || 0), 0);
                    rawDashboard.totalUnits = rawDashboard.projectsOverview.reduce((sum, p) => sum + (Number(p.totalUnits) || 0), 0);
                    rawDashboard.occupiedUnits = rawDashboard.projectsOverview.reduce((sum, p) => sum + (Number(p.occupiedUnits) || 0), 0);
                    rawDashboard.occupancyRate = rawDashboard.totalUnits > 0
                        ? Math.round((rawDashboard.occupiedUnits / rawDashboard.totalUnits) * 100)
                        : 0;
                } catch (reconErr) {
                    console.warn('Reconciling builder dashboard with active projects skipped:', reconErr);
                }
            }

            builderDashboard.value = rawDashboard;
        } catch (error) {
            errors.value.push(error);
            console.error('Error fetching builder dashboard:', error);
        } finally {
            if (!silent) loading.value = false;
        }
    }

    async function fetchOwnerDashboard(ownerId, silent = false) {
        if (!silent) loading.value = true;
        try {
            ownerDashboard.value = await analyticsApi.getOwnerDashboard(ownerId);
        } catch (error) {
            errors.value.push(error);
            console.error('Error fetching owner dashboard:', error);
        } finally {
            if (!silent) loading.value = false;
        }
    }

    async function fetchHistoricalData(projectId, dataType, startDate, endDate) {
        loading.value = true;
        try {
            historicalData.value = await analyticsApi.getHistoricalData(projectId, dataType, startDate, endDate);
        } catch (error) {
            errors.value.push(error);
            console.error('Error fetching historical data:', error);
        } finally {
            loading.value = false;
        }
    }

    async function fetchDevices() {
        try {
            const result = await deviceApi.getAllDevices();
            devices.value = result;
            // Auto-selection is intentionally removed — the component's watch on
            // deviceOptions (filtered by builder's projects) handles selection so
            // that devices from other builders are never auto-selected.
        } catch (error) {
            console.error('Error fetching devices for telemetry:', error);
        }
    }

    async function fetchDeviceEnergy(deviceId, from, to) {
        telemetryLoading.value = true;
        try {
            deviceEnergyReadings.value = await analyticsApi.getDeviceEnergy(deviceId, from, to);
        } catch (error) {
            console.error('Error fetching device energy:', error);
            deviceEnergyReadings.value = [];
        } finally {
            telemetryLoading.value = false;
        }
    }

    async function fetchDeviceStatus(deviceId) {
        try {
            deviceStatus.value = await analyticsApi.getDeviceStatus(deviceId);
        } catch (error) {
            console.error('Error fetching device status:', error);
            deviceStatus.value = null;
        }
    }

    function selectDevice(deviceId) {
        selectedDeviceId.value = deviceId;
        // Clear stale telemetry data when deselecting so the chart/status panel
        // doesn't show readings that belong to a different device or builder.
        if (!deviceId) {
            deviceEnergyReadings.value = [];
            deviceStatus.value = null;
        }
    }

    async function fetchLiveEnergy(userId, role, minutes = ANALYTICS_DEFAULT_MINUTES) {
        liveEnergyLoading.value = true;
        try {
            liveEnergyData.value = await analyticsApi.getLiveEnergy(userId, role, minutes);
        } catch (e) {
            liveEnergyData.value = [];
        } finally {
            liveEnergyLoading.value = false;
        }
    }

    function startLiveEnergyPolling(userId, role, minutes = ANALYTICS_DEFAULT_MINUTES) {
        fetchLiveEnergy(userId, role, minutes);
        _liveEnergyInterval = setInterval(() => fetchLiveEnergy(userId, role, minutes), POLLING_INTERVAL_MS);
    }

    function stopLiveEnergyPolling() {
        if (_liveEnergyInterval) {
            clearInterval(_liveEnergyInterval);
            _liveEnergyInterval = null;
        }
    }

    function clearErrors() {
        errors.value = [];
    }

    function clearUserSession() {
        builderDashboard.value = null;
        ownerDashboard.value = null;
        historicalData.value = [];
        devices.value = [];
        deviceEnergyReadings.value = [];
        deviceStatus.value = null;
        selectedDeviceId.value = null;
        liveEnergyData.value = [];
        errors.value = [];
        stopLiveEnergyPolling();
    }

    /**
     * Invalidate the cached builder dashboard so the next navigation to the
     * analytics view triggers a fresh fetch instead of showing a stale value.
     * Called by project-details.vue after structure is successfully defined.
     */
    function invalidateBuilderDashboard() {
        builderDashboard.value = null;
    }

    return {
        builderDashboard,
        ownerDashboard,
        historicalData,
        loading,
        errors,
        deviceEnergyReadings,
        deviceStatus,
        selectedDeviceId,
        devices,
        telemetryLoading,
        fetchBuilderDashboard,
        fetchOwnerDashboard,
        fetchHistoricalData,
        fetchDevices,
        fetchDeviceEnergy,
        fetchDeviceStatus,
        selectDevice,
        clearErrors,
        clearUserSession,
        invalidateBuilderDashboard,
        liveEnergyData,
        liveEnergyLoading,
        fetchLiveEnergy,
        startLiveEnergyPolling,
        stopLiveEnergyPolling
    };
});

export default useAnalyticsStore;
