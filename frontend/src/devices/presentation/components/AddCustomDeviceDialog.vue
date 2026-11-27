<script setup>
import { reactive, computed, ref, watch, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useAnalyticsStore } from '../../../analytics/application/analytics.store.js';
import { DeviceApi } from '../../infrastructure/device-api.js';
import { DeviceStatus } from '../../domain/model/device-status.enum.js';

/**
 * Dialog for adding a catalog device to an owner's unit (Slice 3 — catalog picker).
 *
 * Flow:
 *  1. Owner picks a device type from the catalog (scope "unit" or "both" only).
 *  2. Owner enters a device name and selects which unit to assign it to.
 *  3. On submit: POST /api/v1/devices with { type, name, location, projectId, unitId }.
 *
 * The two-step custom-type-creation flow (POST /types/custom + POST /devices) is removed.
 * Emits `created` with the new device DTO on success; the parent closes the dialog.
 */

const props = defineProps({
  modelValue: { type: Boolean, default: false },
});

const emit = defineEmits(['update:modelValue', 'created']);

const analyticsStore = useAnalyticsStore();
const { t, locale } = useI18n();

// ── Catalog types ─────────────────────────────────────────────────────────────

const catalogTypes = ref([]);
const catalogLoading = ref(false);

async function loadCatalogTypes() {
  catalogLoading.value = true;
  try {
    const api = new DeviceApi();
    const all = await api.getDeviceTypes();
    // Filter to types that are valid for unit-level devices
    catalogTypes.value = all.filter(
      (t) => t.scope === 'unit' || t.scope === 'both'
    );
  } catch (err) {
    console.error('[AddCustomDeviceDialog] failed to load catalog types:', err);
    catalogTypes.value = [];
  } finally {
    catalogLoading.value = false;
  }
}

const typeOptions = computed(() =>
  catalogTypes.value.map((type) => ({
    label: type.displayName,
    value: type.code,
  }))
);

// ── Unit options ──────────────────────────────────────────────────────────────

/** Units sourced from the owner dashboard payload (myUnitsDetails). */
const unitOptions = computed(() =>
  (analyticsStore.ownerDashboard?.myUnitsDetails ?? []).map((u) => ({
    label: t('devices.addDialog.unitOption', {
      // floor and room as the user knows them; the database id is only a fallback
      unit: u.floor && u.roomNumber
        ? t('analytics.unitCard.label', { floor: u.floor, unit: u.roomNumber })
        : t('analytics.unitCard.fallback', { id: u.unitId }),
      project: u.projectName ?? t('devices.addDialog.unknownProject'),
    }),
    value: u.unitId,
    projectId: u.projectId ?? null,
  }))
);

// ── Form state ────────────────────────────────────────────────────────────────

const form = reactive({
  selectedTypeCode: null,
  deviceName: '',
  selectedUnitId: null,
});

// ── Visibility ────────────────────────────────────────────────────────────────

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val),
});

// Reset form when dialog opens; also (re)load catalog types
watch(visible, (isOpen) => {
  if (isOpen) {
    form.selectedTypeCode = null;
    form.deviceName = '';
    form.selectedUnitId = unitOptions.value[0]?.value ?? null;
    errorMessage.value = '';
    errors.type = '';
    errors.name = '';
    errors.unit = '';
    loadCatalogTypes();
  }
});

onMounted(() => {
  // Pre-load so the picker is populated quickly on first open
  loadCatalogTypes();
});

// ── Inline errors ─────────────────────────────────────────────────────────────

const errorMessage = ref('');
const submitting = ref(false);

const errors = reactive({
  type: '',
  name: '',
  unit: ''
});

// Inline errors store translation keys; a server message is shown as received.
const translate = (value) => (value?.startsWith('devices.') ? t(value) : value);

// ── Validation ────────────────────────────────────────────────────────────────

function validate() {
  errors.type = '';
  errors.name = '';
  errors.unit = '';
  let valid = true;

  if (!form.selectedTypeCode) {
    errors.type = 'devices.addDialog.errors.typeRequired';
    valid = false;
  }
  if (!form.deviceName || form.deviceName.trim().length < 2 || form.deviceName.trim().length > 50) {
    errors.name = 'devices.addDialog.errors.nameLength';
    valid = false;
  }
  if (form.selectedUnitId == null) {
    errors.unit = 'devices.addDialog.errors.unitRequired';
    valid = false;
  }
  return valid;
}

// ── Submit ────────────────────────────────────────────────────────────────────

async function onSubmit() {
  errorMessage.value = '';
  if (!validate()) {
    return;
  }

  submitting.value = true;

  try {
    // Derive projectId from the selected unit — never hardcode 1
    const selectedUnit = unitOptions.value.find((u) => u.value === form.selectedUnitId);
    const projectId = selectedUnit?.projectId ?? null;

    if (!projectId) {
      errorMessage.value = 'devices.addDialog.errors.noProject';
      return;
    }

    const api = new DeviceApi();
    const newDevice = await api.createDevice({
      name: form.deviceName.trim(),
      type: form.selectedTypeCode,
      location: `Unit ${form.selectedUnitId}`,
      projectId,
      status: DeviceStatus.ACTIVE,
      unitId: form.selectedUnitId,
    });

    emit('created', newDevice);
    visible.value = false;
  } catch (err) {
    const status = err?.response?.status;
    // The API answers in English, so its text is only used for the English UI.
    const serverMessage = locale.value === 'en' ? err?.response?.data?.error : undefined;

    if (status === 409) {
      errorMessage.value = serverMessage ?? 'devices.addDialog.errors.duplicate';
    } else if (status === 400) {
      errorMessage.value = serverMessage ?? 'devices.addDialog.errors.validation';
    } else if (status === 403) {
      errorMessage.value = 'devices.addDialog.errors.forbidden';
    } else {
      errorMessage.value = serverMessage ?? 'devices.addDialog.errors.unexpected';
    }
    console.error('[AddCustomDeviceDialog] submit error:', err);
  } finally {
    submitting.value = false;
  }
}

function onCancel() {
  visible.value = false;
}
</script>

<template>
  <pv-dialog
    v-model:visible="visible"
    modal
    :header="t('devices.addDialog.title')"
    :style="{ width: '480px' }"
    contentClass="add-device-dialog"
    @hide="onCancel"
  >
    <div class="dialog-body">

      <!-- Error banner -->
      <pv-message v-if="errorMessage" severity="error" :closable="false" class="error-banner">
        {{ translate(errorMessage) }}
      </pv-message>

      <!-- Device type picker -->
      <div class="field">
        <label class="field-label">{{ t('devices.addDialog.type') }} <span class="required">*</span></label>
        <pv-select
          v-model="form.selectedTypeCode"
          :options="typeOptions"
          option-label="label"
          option-value="value"
          :loading="catalogLoading"
          :placeholder="t('devices.addDialog.typePlaceholder')"
          :invalid="!!errors.type"
          class="field-input"
          @change="errors.type = ''"
        />
        <small v-if="errors.type" class="p-error block mt-1">{{ translate(errors.type) }}</small>
        <small v-else-if="typeOptions.length === 0 && !catalogLoading" class="hint warn">
          {{ t('devices.addDialog.noTypes') }}
        </small>
      </div>

      <!-- Device name (instance label) -->
      <div class="field">
        <label class="field-label">{{ t('devices.addDialog.name') }} <span class="required">*</span></label>
        <pv-input-text
          v-model="form.deviceName"
          :placeholder="t('devices.addDialog.namePlaceholder')"
          maxlength="50"
          :invalid="!!errors.name"
          class="field-input"
          @input="errors.name = ''"
        />
        <small v-if="errors.name" class="p-error block mt-1">{{ translate(errors.name) }}</small>
      </div>

      <!-- Unit picker -->
      <div class="field">
        <label class="field-label">{{ t('devices.addDialog.unit') }} <span class="required">*</span></label>
        <pv-select
          v-model="form.selectedUnitId"
          :options="unitOptions"
          option-label="label"
          option-value="value"
          :placeholder="t('devices.addDialog.unitPlaceholder')"
          :invalid="!!errors.unit"
          class="field-input"
          @change="errors.unit = ''"
        />
        <small v-if="errors.unit" class="p-error block mt-1">{{ translate(errors.unit) }}</small>
        <small v-else-if="unitOptions.length === 0" class="hint warn">
          {{ t('devices.addDialog.noUnits') }}
        </small>
      </div>

    </div>

    <template #footer>
      <div class="dialog-footer">
        <pv-button :label="t('devices.addDialog.cancel')" text @click="onCancel" />
        <pv-button
          :label="t('devices.addDialog.submit')"
          icon="pi pi-check"
          :loading="submitting"
          :disabled="catalogLoading"
          @click="onSubmit"
        />
      </div>
    </template>
  </pv-dialog>
</template>

<style scoped>
.dialog-body {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  padding: 0.25rem 0;
}

:deep(.add-device-dialog) {
  background: #ffffff !important;
  color: #111827 !important;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.field-label {
  font-weight: 500;
  color: #111827;
  font-size: 0.9rem;
}

.field-input {
  width: 100%;
}

.required {
  color: #ef4444;
}

.hint {
  color: #6b7280;
  font-size: 0.75rem;
}

.hint.warn {
  color: #f59e0b;
}

.error-banner {
  margin-bottom: 0.25rem;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  width: 100%;
}
</style>
