<script setup>
import { ref, computed, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import useProjectStore from '../../application/project.store.js';
import { useDeviceStore } from '../../../devices/application/device.store.js';
import useSubscriptionStore from '../../../subscriptions/application/subscription.store.js';
import { useAnalyticsStore } from '../../../analytics/application/analytics.store.js';
import { getPlanLimits } from '../../../subscriptions/domain/model/plan-limits.js';
import { TOAST_INVOICE_ERROR_DURATION_MS, TOAST_AUTH_ERROR_DURATION_MS } from '../../../shared/infrastructure/constants.js';

const props = defineProps({
    visible: {
        type: Boolean,
        required: true
    },
    projectId: {
        type: [Number, String],
        required: true
    }
});

const emit = defineEmits(['update:visible', 'structure-defined']);

const router = useRouter();
const store = useProjectStore();
const deviceStore = useDeviceStore();
const subscriptionStore = useSubscriptionStore();
const analyticsStore = useAnalyticsStore();
const toast = useToast();
const { t, te } = useI18n();

const localVisible = ref(props.visible);
const floors = ref(1);
const unitsPerFloor = ref(1);

const MAX_FLOORS = 50;
const MAX_UNITS_PER_FLOOR = 20;
const MAX_TOTAL_UNITS = 500;

const currentPlanLimits = computed(() => {
    return getPlanLimits(subscriptionStore.currentPlan);
});

const currentDevicesCount = computed(() => {
    return Number(analyticsStore.builderDashboard?.totalDevices) || 0;
});

const totalUnits = computed(() => (Number(floors.value) || 0) * (Number(unitsPerFloor.value) || 0));
const isTotalUnitsExceeded = computed(() => totalUnits.value > MAX_TOTAL_UNITS);

// Estimated new devices created by defining this structure
const estimatedNewDevices = computed(() => {
    const fCount = Number(floors.value) || 0;
    const uCount = Number(unitsPerFloor.value) || 0;
    if (fCount <= 0 || uCount <= 0) return 0;

    let floorDevs = 0;
    for (let f = 1; f <= fCount; f++) {
        const types = deviceTypesByFloor.value[f];
        if (types && types.length > 0) {
            floorDevs += types.length;
        } else {
            floorDevs += 3; // Backend default provision per floor
        }
    }

    let unitDevs = 0;
    const totalU = fCount * uCount;
    let customCount = 0;
    for (const [key, types] of Object.entries(unitDevicePackages.value)) {
        if (types && types.length > 0) {
            const [fStr] = key.split('-');
            const fNum = parseInt(fStr, 10);
            if (fNum <= fCount) {
                unitDevs += types.length;
                customCount++;
            }
        }
    }
    const defaultUnits = Math.max(0, totalU - customCount);
    unitDevs += defaultUnits * 2; // Backend default provision per unit

    return floorDevs + unitDevs;
});

const projectedTotalDevices = computed(() => {
    return currentDevicesCount.value + estimatedNewDevices.value;
});

const isPlanDeviceLimitExceeded = computed(() => {
    if (currentPlanLimits.value.isUnlimited || currentPlanLimits.value.maxDevices === Infinity) {
        return false;
    }
    return projectedTotalDevices.value > currentPlanLimits.value.maxDevices;
});

const isAlreadyAtOrOverLimit = computed(() => {
    if (currentPlanLimits.value.isUnlimited || currentPlanLimits.value.maxDevices === Infinity) {
        return false;
    }
    return currentDevicesCount.value >= currentPlanLimits.value.maxDevices;
});

const isInvalidStructure = computed(() => {
    const f = Number(floors.value);
    const u = Number(unitsPerFloor.value);
    return (
        !f ||
        f < 1 ||
        f > MAX_FLOORS ||
        !u ||
        u < 1 ||
        u > MAX_UNITS_PER_FLOOR ||
        isTotalUnitsExceeded.value ||
        isPlanDeviceLimitExceeded.value
    );
});

// UI-only presentation state: which floor accordion panels are open.
const expandedFloors = ref(new Set([1]));
const submitting = ref(false);

// Per-floor device-type selections — keyed by floor number (integer)
const deviceTypesByFloor = ref({});

// Per-unit device package selections — keyed by "floor-roomNumber" e.g. "1-01"
const unitDevicePackages = ref({});

watch(() => props.visible, (val) => {
    localVisible.value = val;
    if (val) {
        floors.value = 1;
        unitsPerFloor.value = 1;
        expandedFloors.value = new Set([1]);
        deviceTypesByFloor.value = {};
        unitDevicePackages.value = {};
        deviceStore.loadDeviceTypes();
        if (!subscriptionStore.currentPlan && !subscriptionStore.isLoading) {
            subscriptionStore.loadSubscriptions();
        }
        if (!analyticsStore.builderDashboard) {
            analyticsStore.loadBuilderDashboard();
        }
    }
});

function navigateToSubscriptions() {
    localVisible.value = false;
    router.push({ name: 'my-subscription' });
}

watch(localVisible, (val) => {
    emit('update:visible', val);
});

function getDeviceName(device) {
    if (!device) return '';
    const key = `projects.deviceCatalog.${device.code}`;
    return te(key) ? t(key) : device.displayName;
}

// Catalog types scoped to floors: scope "floor" or "both" (hides unit-only types).
const floorDeviceTypes = computed(() =>
    deviceStore.deviceTypes
        .filter(d => d.scope === 'floor' || d.scope === 'both')
        .map(d => ({
            ...d,
            displayName: getDeviceName(d)
        }))
);

// Catalog types scoped to units: scope "unit" or "both" (hides floor-only types).
const unitDeviceTypes = computed(() =>
    deviceStore.deviceTypes
        .filter(d => d.scope === 'unit' || d.scope === 'both')
        .map(d => ({
            ...d,
            displayName: getDeviceName(d)
        }))
);

// Stepper input helpers — prevent typing non-numeric characters and enforce maxlength/bounds in real-time
function onNumericBeforeInput(event) {
    if (event.data && !/^\d+$/.test(event.data)) {
        event.preventDefault();
    }
}

function onFloorsInput(event) {
    let val = event.target.value.replace(/\D/g, '');
    if (val.length > 2) {
        val = val.slice(0, 2);
    }
    if (val === '') {
        floors.value = '';
        event.target.value = '';
        return;
    }
    let num = parseInt(val, 10);
    if (num > MAX_FLOORS) {
        num = MAX_FLOORS;
    }
    floors.value = num;
    event.target.value = String(num);
}

function onFloorsBlur(event) {
    if (!floors.value || Number(floors.value) < 1) {
        floors.value = 1;
    }
    event.target.value = String(floors.value);
}

function onUnitsInput(event) {
    let val = event.target.value.replace(/\D/g, '');
    if (val.length > 2) {
        val = val.slice(0, 2);
    }
    if (val === '') {
        unitsPerFloor.value = '';
        event.target.value = '';
        return;
    }
    let num = parseInt(val, 10);
    if (num > MAX_UNITS_PER_FLOOR) {
        num = MAX_UNITS_PER_FLOOR;
    }
    unitsPerFloor.value = num;
    event.target.value = String(num);
}

function onUnitsBlur(event) {
    if (!unitsPerFloor.value || Number(unitsPerFloor.value) < 1) {
        unitsPerFloor.value = 1;
    }
    event.target.value = String(unitsPerFloor.value);
}

function changeFloors(delta) {
    const current = Number(floors.value) || 1;
    const next = Math.min(MAX_FLOORS, Math.max(1, current + delta));
    floors.value = next;
}

function changeUnitsPerFloor(delta) {
    const current = Number(unitsPerFloor.value) || 1;
    const next = Math.min(MAX_UNITS_PER_FLOOR, Math.max(1, current + delta));
    unitsPerFloor.value = next;
}

// Recompute grid whenever floors/unitsPerFloor change; clear stale keys
const unitGrid = computed(() => {
    const fCount = Number(floors.value) || 0;
    const uCount = Number(unitsPerFloor.value) || 0;
    const grid = [];
    for (let f = 1; f <= fCount; f++) {
        const units = [];
        for (let u = 1; u <= uCount; u++) {
            const roomNumber = String(u).padStart(2, '0');
            units.push({ floor: f, roomNumber });
        }
        grid.push({ floor: f, units });
    }
    return grid;
});

watch([floors, unitsPerFloor], () => {
    const fCount = Number(floors.value) || 0;
    const uCount = Number(unitsPerFloor.value) || 0;
    const nextDeviceTypes = {};
    const nextUnitPackages = {};
    for (let f = 1; f <= fCount; f++) {
        // Preserve device-type selections for floors still in range
        if (deviceTypesByFloor.value[f] !== undefined) {
            nextDeviceTypes[f] = deviceTypesByFloor.value[f];
        }
        for (let u = 1; u <= uCount; u++) {
            const room = String(u).padStart(2, '0');
            const key = `${f}-${room}`;
            if (unitDevicePackages.value[key] !== undefined) {
                nextUnitPackages[key] = unitDevicePackages.value[key];
            }
        }
    }
    deviceTypesByFloor.value = nextDeviceTypes;
    unitDevicePackages.value = nextUnitPackages;
});

function unitKey(floor, roomNumber) {
    return `${floor}-${roomNumber}`;
}

// Accordion open/close — presentation only, does not touch the payload.
function toggleFloor(floor) {
    const next = new Set(expandedFloors.value);
    next.has(floor) ? next.delete(floor) : next.add(floor);
    expandedFloors.value = next;
}

function isFloorExpanded(floor) {
    return expandedFloors.value.has(floor);
}

// Count of floor-wide device types selected for a floor (header summary).
function floorDeviceCount(floor) {
    return deviceTypesByFloor.value[floor]?.length ?? 0;
}

function buildPayload() {
    const fCount = Number(floors.value) || 1;
    const uCount = Number(unitsPerFloor.value) || 1;
    // Only include floors where the builder explicitly selected device types.
    // Sending [] was interpreted by the backend as "use legacy defaults", which
    // created default devices on every floor even when the builder made no selection,
    // causing phantom device inflation (N floors × default count).
    const deviceTypesPerFloor = [];
    for (let f = 1; f <= fCount; f++) {
        const types = deviceTypesByFloor.value[f];
        if (types && types.length > 0) {
            deviceTypesPerFloor.push({ floor: f, deviceTypes: types });
        }
    }

    // Unit device packages — only include units with at least one type selected
    const unitPackages = [];
    for (const [key, deviceTypes] of Object.entries(unitDevicePackages.value)) {
        if (deviceTypes && deviceTypes.length > 0) {
            const [floorStr, roomNumber] = key.split('-');
            unitPackages.push({
                floor: parseInt(floorStr),
                roomNumber,
                deviceTypes
            });
        }
    }

    return {
        floors: fCount,
        unitsPerFloor: uCount,
        ownerEmails: [],
        deviceTypesPerFloor,
        unitDevicePackages: unitPackages
    };
}

async function handleSubmit() {
    const fCount = Number(floors.value) || 0;
    const uCount = Number(unitsPerFloor.value) || 0;

    if (fCount < 1 || fCount > MAX_FLOORS) {
        toast.add({
            severity: 'warn',
            summary: 'Límite de pisos excedido',
            detail: `El número de pisos debe estar entre 1 y ${MAX_FLOORS}.`,
            life: TOAST_INVOICE_ERROR_DURATION_MS
        });
        return;
    }

    if (uCount < 1 || uCount > MAX_UNITS_PER_FLOOR) {
        toast.add({
            severity: 'warn',
            summary: 'Límite de unidades excedido',
            detail: `El número de unidades por piso debe estar entre 1 y ${MAX_UNITS_PER_FLOOR}.`,
            life: TOAST_INVOICE_ERROR_DURATION_MS
        });
        return;
    }

    if (totalUnits.value > MAX_TOTAL_UNITS) {
        toast.add({
            severity: 'warn',
            summary: 'Límite total excedido',
            detail: `El total de departamentos no puede superar los ${MAX_TOTAL_UNITS} (actualmente: ${totalUnits.value}).`,
            life: TOAST_INVOICE_ERROR_DURATION_MS
        });
        return;
    }

    if (isPlanDeviceLimitExceeded.value) {
        toast.add({
            severity: 'error',
            summary: te('subscriptions.quotaExceededTitle') ? t('subscriptions.quotaExceededTitle') : 'Límite de dispositivos excedido',
            detail: te('subscriptions.quotaExceededToast')
                ? t('subscriptions.quotaExceededToast', { max: currentPlanLimits.value.maxDevices, plan: currentPlanLimits.value.name })
                : `Esta estructura excede el límite de ${currentPlanLimits.value.maxDevices} dispositivos de su plan ${currentPlanLimits.value.name}. Por favor actualice su suscripción.`,
            life: TOAST_AUTH_ERROR_DURATION_MS
        });
        return;
    }

    submitting.value = true;
    try {
        const payload = buildPayload();
        await store.defineProjectStructure(props.projectId, payload);
        toast.add({
            severity: 'success',
            summary: 'Structure defined',
            detail: `${fCount} floor(s) × ${uCount} unit(s) created successfully.`,
            life: TOAST_INVOICE_ERROR_DURATION_MS
        });
        localVisible.value = false;
        emit('structure-defined');
    } catch (error) {
        const status = error?.response?.status;
        if (status === 409) {
            toast.add({
                severity: 'warn',
                summary: 'Structure already defined',
                detail: 'This project already has a structure. You cannot redefine it.',
                life: TOAST_AUTH_ERROR_DURATION_MS
            });
        } else if (status === 403) {
            toast.add({
                severity: 'error',
                summary: 'Access denied',
                detail: 'Only builders can define project structure.',
                life: TOAST_AUTH_ERROR_DURATION_MS
            });
        } else if (status === 422) {
            toast.add({
                severity: 'error',
                summary: 'Validation error',
                detail: error?.response?.data?.error || `Datos inválidos. Mínimo 1 y máximo ${MAX_FLOORS} pisos, ${MAX_UNITS_PER_FLOOR} unidades por piso (máx. ${MAX_TOTAL_UNITS} en total).`,
                life: TOAST_AUTH_ERROR_DURATION_MS
            });
        } else {
            toast.add({
                severity: 'error',
                summary: 'Error',
                detail: 'An unexpected error occurred. Please try again.',
                life: TOAST_AUTH_ERROR_DURATION_MS
            });
        }
    } finally {
        submitting.value = false;
    }
}

function handleCancel() {
    localVisible.value = false;
}
</script>

<template>
    <pv-dialog
        v-model:visible="localVisible"
        modal
        :header="te('projects.structure.dialog-title') ? t('projects.structure.dialog-title') : (te('projects.actions.define-structure') ? t('projects.actions.define-structure') : 'Configurar Estructura del Proyecto')"
        :style="{ width: '880px', maxWidth: '96vw', maxHeight: '90vh' }"
        class="define-structure-dialog"
    >
        <div class="ds-body">
            <!-- Floors & Units per floor -->
            <div class="ds-grid2">
                <div class="ds-field">
                    <label class="ds-label">
                        <i class="pi pi-building"></i>
                        {{ te('projects.fields.floors') ? t('projects.fields.floors') : 'Pisos' }}
                    </label>
                    <div class="custom-stepper">
                        <button
                            type="button"
                            class="custom-stepper__btn custom-stepper__btn--dec"
                            :disabled="Number(floors) <= 1"
                            @click="changeFloors(-1)"
                            aria-label="Disminuir pisos"
                        >
                            <i class="pi pi-minus"></i>
                        </button>
                        <input
                            type="text"
                            inputmode="numeric"
                            pattern="[0-9]*"
                            maxlength="2"
                            class="custom-stepper__input"
                            :value="floors"
                            @beforeinput="onNumericBeforeInput"
                            @input="onFloorsInput"
                            @blur="onFloorsBlur"
                            @keydown.up.prevent="changeFloors(1)"
                            @keydown.down.prevent="changeFloors(-1)"
                            placeholder="1"
                        />
                        <button
                            type="button"
                            class="custom-stepper__btn custom-stepper__btn--inc"
                            :disabled="Number(floors) >= MAX_FLOORS"
                            @click="changeFloors(1)"
                            aria-label="Aumentar pisos"
                        >
                            <i class="pi pi-plus"></i>
                        </button>
                    </div>
                    <small class="ds-hint">{{ te('projects.structure.floors-hint') ? t('projects.structure.floors-hint', { max: MAX_FLOORS }) : `Mínimo 1, máximo ${MAX_FLOORS} pisos` }}</small>
                </div>
                <div class="ds-field">
                    <label class="ds-label">
                        <i class="pi pi-th-large"></i>
                        {{ te('projects.fields.units-per-floor') ? t('projects.fields.units-per-floor') : 'Unidades por piso' }}
                    </label>
                    <div class="custom-stepper">
                        <button
                            type="button"
                            class="custom-stepper__btn custom-stepper__btn--dec"
                            :disabled="Number(unitsPerFloor) <= 1"
                            @click="changeUnitsPerFloor(-1)"
                            aria-label="Disminuir unidades por piso"
                        >
                            <i class="pi pi-minus"></i>
                        </button>
                        <input
                            type="text"
                            inputmode="numeric"
                            pattern="[0-9]*"
                            maxlength="2"
                            class="custom-stepper__input"
                            :value="unitsPerFloor"
                            @beforeinput="onNumericBeforeInput"
                            @input="onUnitsInput"
                            @blur="onUnitsBlur"
                            @keydown.up.prevent="changeUnitsPerFloor(1)"
                            @keydown.down.prevent="changeUnitsPerFloor(-1)"
                            placeholder="1"
                        />
                        <button
                            type="button"
                            class="custom-stepper__btn custom-stepper__btn--inc"
                            :disabled="Number(unitsPerFloor) >= MAX_UNITS_PER_FLOOR"
                            @click="changeUnitsPerFloor(1)"
                            aria-label="Aumentar unidades por piso"
                        >
                            <i class="pi pi-plus"></i>
                        </button>
                    </div>
                    <small class="ds-hint">{{ te('projects.structure.units-hint') ? t('projects.structure.units-hint', { max: MAX_UNITS_PER_FLOOR }) : `Mínimo 1, máximo ${MAX_UNITS_PER_FLOOR} por piso` }}</small>
                </div>
            </div>

            <!-- Summary & Quota Status -->
            <div
                class="ds-summary"
                :class="{
                    'ds-summary--danger': isPlanDeviceLimitExceeded || isTotalUnitsExceeded,
                    'ds-summary--warning': !isPlanDeviceLimitExceeded && !isTotalUnitsExceeded && !currentPlanLimits.isUnlimited && projectedTotalDevices >= (currentPlanLimits.maxDevices * 0.8)
                }"
            >
                <i :class="(isPlanDeviceLimitExceeded || isTotalUnitsExceeded) ? 'pi pi-exclamation-circle' : 'pi pi-info-circle'"></i>
                <div class="ds-summary__content">
                    <div>
                        {{ te('projects.structure.summary-prefix') ? t('projects.structure.summary-prefix') : 'Esto creará' }}
                        <strong>{{ totalUnits }}</strong>
                        {{ te('projects.structure.units-label') ? t('projects.structure.units-label') : 'departamento(s)' }}:
                        {{ te('projects.structure.summary-detail') ? t('projects.structure.summary-detail', { floors: floors || 0, units: unitsPerFloor || 0 }) : `${floors || 0} piso(s) × ${unitsPerFloor || 0} departamento(s) por piso.` }}
                    </div>

                    <!-- Quota calculation line -->
                    <div class="ds-quota-line">
                        <span>{{ te('subscriptions.usage-devices') ? t('subscriptions.usage-devices') : 'Dispositivos IoT' }}:</span>
                        <strong>{{ te('subscriptions.quotaDevicesCalc') ? t('subscriptions.quotaDevicesCalc', { current: currentDevicesCount, newDevs: estimatedNewDevices, total: projectedTotalDevices, max: currentPlanLimits.label }) : `${currentDevicesCount} en uso + ~${estimatedNewDevices} nuevos = ${projectedTotalDevices} / ${currentPlanLimits.label}` }}</strong>
                        <span class="ds-quota-badge" :class="{ 'ds-quota-badge--danger': isPlanDeviceLimitExceeded }">
                            Plan {{ currentPlanLimits.name }}
                        </span>
                    </div>

                    <div v-if="isTotalUnitsExceeded" class="ds-error-text">
                        {{ te('projects.structure.max-units-exceeded') ? t('projects.structure.max-units-exceeded', { max: MAX_TOTAL_UNITS }) : `Supera el límite máximo permitido de ${MAX_TOTAL_UNITS} departamentos por proyecto.` }}
                    </div>

                    <div v-if="isPlanDeviceLimitExceeded" class="ds-error-text">
                        <span v-if="isAlreadyAtOrOverLimit">
                            {{ te('subscriptions.quotaAlreadyExceededMsg') ? t('subscriptions.quotaAlreadyExceededMsg', { current: currentDevicesCount, max: currentPlanLimits.label }) : `Ya has alcanzado o superado el límite de tu plan (${currentDevicesCount}/${currentPlanLimits.label} dispositivos). Actualiza tu plan para definir más estructuras.` }}
                        </span>
                        <span v-else>
                            {{ te('subscriptions.quotaWillExceedMsg') ? t('subscriptions.quotaWillExceedMsg', { projected: projectedTotalDevices, max: currentPlanLimits.label }) : `Esta estructura resultará en ~${projectedTotalDevices} dispositivos, superando el límite de ${currentPlanLimits.label} dispositivos de tu plan.` }}
                        </span>
                        <div>
                            <button type="button" class="ds-quota-upgrade-link" @click="navigateToSubscriptions">
                                <i class="pi pi-arrow-up-right"></i>
                                {{ te('subscriptions.upgrade') ? t('subscriptions.upgrade') : 'Actualizar Plan de Suscripción' }}
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Per-floor configuration (accordion: one panel per floor) -->
            <div v-if="deviceStore.deviceTypes.length > 0">
                <p class="ds-section-title">
                    <i class="pi pi-sitemap"></i>
                    {{ te('projects.structure.configure-each-floor') ? t('projects.structure.configure-each-floor') : 'Configurar cada piso' }}
                </p>

                <div class="ds-accordion">
                    <div
                        v-for="row in unitGrid"
                        :key="row.floor"
                        class="floor-panel"
                    >
                        <!-- Floor header (toggle) -->
                        <button
                            type="button"
                            class="floor-panel__head"
                            @click="toggleFloor(row.floor)"
                        >
                            <i
                                class="pi floor-panel__chevron"
                                :class="isFloorExpanded(row.floor) ? 'pi-chevron-down' : 'pi-chevron-right'"
                            ></i>
                            <span class="floor-panel__name">{{ te('projects.structure.floor') ? t('projects.structure.floor', { number: row.floor }) : `Piso ${row.floor}` }}</span>
                            <span class="floor-panel__meta">
                                <span>{{ row.units.length }} {{ row.units.length === 1 ? (te('projects.structure.unit-single') ? t('projects.structure.unit-single') : 'unidad') : (te('projects.structure.unit-plural') ? t('projects.structure.unit-plural') : 'unidades') }}</span>
                                <span v-if="floorDeviceCount(row.floor) > 0" class="floor-panel__badge">
                                    <i class="pi pi-wifi"></i>
                                    {{ floorDeviceCount(row.floor) }}
                                </span>
                            </span>
                        </button>

                        <!-- Floor body -->
                        <div v-if="isFloorExpanded(row.floor)" class="floor-panel__body">
                            <!-- Floor-wide IoT devices -->
                            <div class="ds-field">
                                <label class="ds-sublabel">
                                    <i class="pi pi-wifi"></i>
                                    {{ te('projects.structure.floor-iot-devices') ? t('projects.structure.floor-iot-devices') : 'Dispositivos IoT por piso' }}
                                </label>
                                <pv-multi-select
                                    v-model="deviceTypesByFloor[row.floor]"
                                    :options="floorDeviceTypes"
                                    option-label="displayName"
                                    option-value="code"
                                    :placeholder="te('projects.structure.default-devices-placeholder') ? t('projects.structure.default-devices-placeholder') : 'Dispositivos por defecto (3)'"
                                    class="w-full"
                                    display="chip"
                                    :showToggleAll="false"
                                />
                            </div>

                            <!-- Per-unit configuration -->
                            <div>
                                <p class="ds-units-title">{{ te('projects.structure.units') ? t('projects.structure.units') : 'Unidades' }}</p>
                                <div class="ds-units">
                                    <div
                                        v-for="unit in row.units"
                                        :key="unitKey(unit.floor, unit.roomNumber)"
                                        class="unit-block"
                                    >
                                        <span class="unit-block__no">{{ te('projects.structure.unit') ? t('projects.structure.unit', { number: unit.roomNumber }) : `Unidad ${unit.roomNumber}` }}</span>
                                        <pv-multi-select
                                            v-model="unitDevicePackages[unitKey(unit.floor, unit.roomNumber)]"
                                            :options="unitDeviceTypes"
                                            option-label="displayName"
                                            option-value="code"
                                            :placeholder="te('projects.structure.unit-devices-placeholder') ? t('projects.structure.unit-devices-placeholder') : 'Dispositivos unidad (opcional)'"
                                            class="w-full"
                                            display="chip"
                                            :showToggleAll="false"
                                        />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <template #footer>
            <pv-button
                :label="te('projects.actions.cancel') ? t('projects.actions.cancel') : 'Cancelar'"
                icon="pi pi-times"
                @click="handleCancel"
                severity="secondary"
                outlined
            />
            <pv-button
                :label="te('projects.actions.define-structure') ? t('projects.actions.define-structure') : 'Configurar Estructura'"
                icon="pi pi-check"
                @click="handleSubmit"
                :loading="submitting"
                :disabled="isInvalidStructure || submitting"
                class="custom-green-button"
                severity="success"
            />
        </template>
    </pv-dialog>
</template>

<style scoped>
/* ── Dialog shell (PrimeVue overrides) ─────────────────────────────── */
:deep(.p-dialog) { background: #ffffff !important; }
/* Robust height cap: header + footer stay pinned, only the body scrolls.
   Guarantees the footer (Define Structure button) is always on screen,
   no matter how many floors/units are configured.
   Uses the generic .p-dialog selector — same pattern as the background
   rules above that are known to apply; the custom-class selector did not match. */
:deep(.p-dialog) {
    display: flex;
    flex-direction: column;
    max-height: 90vh;
}
:deep(.p-dialog .p-dialog-content) {
    flex: 1 1 auto;
    min-height: 0;
    overflow-y: auto;
}
:deep(.p-dialog .p-dialog-header),
:deep(.p-dialog .p-dialog-footer) {
    flex-shrink: 0;
}
:deep(.p-dialog .p-dialog-header),
:deep(.p-dialog .p-dialog-content),
:deep(.p-dialog .p-dialog-footer) {
    background: #ffffff !important;
    color: #111827 !important;
}
:deep(.p-inputtext) {
    background: #ffffff !important;
    color: #111827 !important;
    border-color: #d1d5db !important;
}
:deep(.p-inputnumber-input) {
    background: #ffffff !important;
    color: #111827 !important;
}
/* Chips wrap instead of overflowing horizontally. */
:deep(.p-multiselect) { width: 100%; }
:deep(.p-multiselect-label) {
    white-space: normal;
    display: flex;
    flex-wrap: wrap;
    gap: 0.25rem;
}

/* ── Content layout ────────────────────────────────────────────────── */
.ds-body {
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
    padding-top: 0.75rem;
    padding-bottom: 0.25rem;
}

.ds-grid2 {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
}

.ds-field { display: flex; flex-direction: column; }

/* ── Custom Stepper ─────────────────────────────────────────────────── */
.custom-stepper {
    display: flex;
    align-items: center;
    border: 1px solid #d1d5db;
    border-radius: 0.5rem;
    overflow: hidden;
    background: #ffffff;
    transition: border-color 0.2s, box-shadow 0.2s;
    height: 42px;
}

.custom-stepper:focus-within {
    border-color: #10b981;
    box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.2);
}

.custom-stepper__btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 42px;
    height: 100%;
    border: none;
    background: #f9fafb;
    color: #4b5563;
    font-size: 0.85rem;
    cursor: pointer;
    transition: background-color 0.15s, color 0.15s;
    user-select: none;
    flex-shrink: 0;
}

.custom-stepper__btn:hover:not(:disabled) {
    background: #e5e7eb;
    color: #111827;
}

.custom-stepper__btn:active:not(:disabled) {
    background: #d1d5db;
}

.custom-stepper__btn:disabled {
    opacity: 0.45;
    cursor: not-allowed;
    background: #f3f4f6;
}

.custom-stepper__btn--dec {
    border-right: 1px solid #e5e7eb;
}

.custom-stepper__btn--inc {
    border-left: 1px solid #e5e7eb;
}

.custom-stepper__input {
    flex: 1;
    min-width: 0;
    height: 100%;
    border: none;
    outline: none;
    text-align: center;
    font-size: 1rem;
    font-weight: 600;
    color: #1f2937;
    background: transparent;
    padding: 0 0.5rem;
}

.custom-stepper__input::placeholder {
    color: #9ca3af;
    font-weight: normal;
}

.ds-hint {
    margin-top: 0.35rem;
    font-size: 0.78rem;
    color: #6b7280;
}

.ds-label {
    display: inline-flex;
    align-items: center;
    gap: 0.45rem;
    margin-bottom: 0.5rem;
    font-weight: 600;
    color: #374151;
}

.ds-label .pi { color: #059669; }

/* Summary callout. */
.ds-summary {
    display: flex;
    align-items: flex-start;
    gap: 0.5rem;
    padding: 0.75rem 1rem;
    background: #ecfdf5;
    border-left: 4px solid #10b981;
    border-radius: 0.55rem;
    font-size: 0.85rem;
    color: #065f46;
}

.ds-summary .pi { margin-top: 0.1rem; color: #059669; }

.ds-summary--warning {
    background: #fffbeb !important;
    border-left-color: #f59e0b !important;
    color: #92400e !important;
}

.ds-summary--warning .pi {
    color: #d97706 !important;
}

.ds-summary--danger {
    background: #fef2f2 !important;
    border-left-color: #ef4444 !important;
    color: #991b1b !important;
}

.ds-summary--danger .pi {
    color: #dc2626 !important;
}

.ds-summary__content {
    flex: 1;
}

.ds-quota-line {
    margin-top: 0.35rem;
    display: flex;
    align-items: center;
    gap: 0.4rem;
    flex-wrap: wrap;
    font-size: 0.8rem;
}

.ds-quota-badge {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    font-size: 0.72rem;
    font-weight: 700;
    padding: 0.15rem 0.5rem;
    border-radius: 9999px;
    background: #e2e8f0;
    color: #334155;
}

.ds-quota-badge--danger {
    background: #fee2e2;
    color: #b91c1c;
}

.ds-quota-upgrade-link {
    margin-top: 0.4rem;
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
    font-size: 0.78rem;
    font-weight: 700;
    color: #dc2626;
    cursor: pointer;
    text-decoration: underline;
    background: none;
    border: none;
    padding: 0;
    font-family: inherit;
}

.ds-quota-upgrade-link:hover {
    color: #991b1b;
}

.ds-error-text {
    margin-top: 0.35rem;
    font-weight: 600;
    color: #dc2626;
    line-height: 1.4;
}

.ds-section-title {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    margin: 0 0 0.75rem;
    font-size: 0.9rem;
    font-weight: 600;
    color: #374151;
}

.ds-section-title .pi { color: #059669; }

/* ── Accordion ─────────────────────────────────────────────────────── */
.ds-accordion {
    display: flex;
    flex-direction: column;
    gap: 0.6rem;
}

.floor-panel {
    border: 1px solid #e5e7eb;
    border-radius: 0.7rem;
    overflow: hidden;
}

.floor-panel__head {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    width: 100%;
    padding: 0.7rem 1rem;
    background: #f9fafb;
    border: none;
    cursor: pointer;
    text-align: left;
    transition: background 0.15s ease;
}

.floor-panel__head:hover { background: #f3f4f6; }

.floor-panel__chevron {
    font-size: 0.8rem;
    color: #059669;
}

.floor-panel__name {
    font-weight: 600;
    color: #1f2937;
}

.floor-panel__meta {
    display: inline-flex;
    align-items: center;
    gap: 0.75rem;
    margin-left: auto;
    font-size: 0.75rem;
    color: #6b7280;
}

.floor-panel__badge {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    font-weight: 600;
    color: #047857;
}

.floor-panel__badge .pi { font-size: 0.65rem; }

.floor-panel__body {
    display: flex;
    flex-direction: column;
    gap: 1rem;
    padding: 1rem;
    border-top: 1px solid #f1f3f5;
}

.ds-sublabel {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    margin-bottom: 0.4rem;
    font-size: 0.75rem;
    font-weight: 600;
    color: #4b5563;
}

.ds-sublabel .pi { color: #059669; }

.ds-units-title {
    margin: 0 0 0.5rem;
    font-size: 0.7rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: #9ca3af;
}

.ds-units {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
    gap: 0.6rem;
}

.unit-block {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding: 0.65rem 0.75rem;
    background: #f9fafb;
    border-radius: 0.55rem;
}

.unit-block__no {
    font-size: 0.75rem;
    font-weight: 600;
    color: #374151;
}
</style>
