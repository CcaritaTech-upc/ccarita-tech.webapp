<script setup>
import { ref, watch, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { Client } from '../../domain/model/client.entity.js';
import { ProjectsFacade } from '../../infrastructure/projects.facade.js';
import {
  isValidEmail,
  isValidPhone,
  validateClientFullName,
  validateClientEmail,
  validateClientAddress
} from '../../../shared/presentation/validators.js';
import { useIamStore } from '../../../iam/application/iam.store.js';

const props = defineProps({
  visible: {
    type: Boolean,
    required: true
  }
});

const emit = defineEmits(['update:visible', 'save']);

const { t } = useI18n();
const toast = useToast();

const iamStore = useIamStore();
const projectsFacade = new ProjectsFacade();
const localVisible = ref(props.visible);
const errors = ref({});
const projects = ref([]);
const units = ref([]);
const loadingUnits = ref(false);
const defaultFormData = () => ({
  fullName: '',
  email: '',
  phoneNumber: '',
  address: '',
  projectId: null,
  projectName: '',
  accountStatement: 'Active',
  builderId: iamStore.currentUser?.id || 0,
  unitId: null,
  unitNumber: ''
});
const formData = ref(defaultFormData());

// Load projects on component mount
onMounted(async () => {
  await projectsFacade.fetchProjects();
  projects.value = projectsFacade.getProjects();
});

// Computed property to format projects for dropdown
const projectOptions = computed(() => {
  return projects.value.map(project => ({
    label: project.name,
    value: project.id
  }));
});

// Computed property to format units for dropdown
const unitOptions = computed(() => {
  const unassignedLabel = t('clients.fields.unassigned');
  const list = [
    { label: unassignedLabel, value: null }
  ];
  units.value.forEach(u => {
    const isOccupied = !!u.ownerEmail;
    const occupiedText = t('projects.structure.occupied');
    const availableText = t('projects.structure.available');
    const statusText = isOccupied ? `(${occupiedText} - ${u.ownerEmail})` : `(${availableText})`;
    const unitText = t('projects.structure.unit', { number: u.unitNumber || u.roomNumber });
    const floorText = t('projects.structure.floor', { number: u.floor });
    list.push({
      label: `${unitText} - ${floorText} ${statusText}`,
      value: u.id
    });
  });
  return list;
});

const touched = ref({
  fullName: false,
  email: false,
  phoneNumber: false,
  address: false,
  projectId: false
});

watch(() => props.visible, (newVal) => {
  localVisible.value = newVal;
  errors.value = {};
  touched.value = {
    fullName: false,
    email: false,
    phoneNumber: false,
    address: false,
    projectId: false
  };
  if (newVal) {
    // Reset form when dialog opens
    formData.value = defaultFormData();
    units.value = [];
  }
});

watch(localVisible, (newVal) => {
  emit('update:visible', newVal);
});

// Watch for project selection to update projectName and fetch units
watch(() => formData.value.projectId, async (newProjectId) => {
  formData.value.unitId = null;
  formData.value.unitNumber = '';
  if (newProjectId) {
    formData.value.projectName = projectsFacade.getProjectNameById(newProjectId);
    loadingUnits.value = true;
    try {
      units.value = await projectsFacade.getUnitsByProject(newProjectId);
    } catch (e) {
      console.error('Error fetching units for project:', e);
      units.value = [];
    } finally {
      loadingUnits.value = false;
    }
  } else {
    formData.value.projectName = '';
    units.value = [];
  }
});

// Watch unit selection to store unitNumber
watch(() => formData.value.unitId, (newUnitId) => {
  if (newUnitId) {
    const selected = units.value.find(u => u.id === newUnitId);
    formData.value.unitNumber = selected ? (selected.unitNumber || selected.roomNumber) : '';
  } else {
    formData.value.unitNumber = '';
  }
});

const validateField = (field, customVal) => {
  if (field === 'fullName') {
    const val = customVal !== undefined ? customVal : formData.value.fullName;
    const res = validateClientFullName(val, t);
    errors.value.fullName = res.isValid ? '' : res.error;
  } else if (field === 'email') {
    const val = customVal !== undefined ? customVal : formData.value.email;
    const res = validateClientEmail(val, t);
    errors.value.email = res.isValid ? '' : res.error;
  } else if (field === 'phoneNumber') {
    const raw = customVal !== undefined ? customVal : formData.value.phoneNumber;
    const val = (raw || '').trim();
    if (val && !isValidPhone(val)) {
      errors.value.phoneNumber = t('clients.validation.phoneInvalid');
    } else {
      errors.value.phoneNumber = '';
    }
  } else if (field === 'address') {
    const val = customVal !== undefined ? customVal : formData.value.address;
    const res = validateClientAddress(val, t);
    errors.value.address = res.isValid ? '' : res.error;
  } else if (field === 'projectId') {
    const val = customVal !== undefined ? customVal : formData.value.projectId;
    if (!val) {
      errors.value.projectId = t('clients.validation.projectRequired');
    } else {
      errors.value.projectId = '';
    }
  }
};

const onFieldInput = (field, val) => {
  touched.value[field] = true;
  if (val !== undefined && formData.value) {
    formData.value[field] = val;
  }
  validateField(field, val);
};

const onFieldBlur = (field) => {
  touched.value[field] = true;
  validateField(field);
};

// Real-time reactive watchers on form data inputs
watch(() => formData.value.fullName, (newVal) => {
  if (touched.value.fullName || (newVal && newVal.length > 0)) {
    validateField('fullName', newVal);
  }
});

watch(() => formData.value.email, (newVal) => {
  if (touched.value.email || (newVal && newVal.length > 0)) {
    validateField('email', newVal);
  }
});

watch(() => formData.value.phoneNumber, (newVal) => {
  if (touched.value.phoneNumber || (newVal && newVal.length > 0)) {
    validateField('phoneNumber', newVal);
  }
});

watch(() => formData.value.address, (newVal) => {
  if (touched.value.address || (newVal && newVal.length > 0)) {
    validateField('address', newVal);
  }
});

const handleSave = () => {
  touched.value = {
    fullName: true,
    email: true,
    phoneNumber: true,
    address: true,
    projectId: true
  };
  errors.value = {};

  validateField('fullName');
  validateField('email');
  validateField('phoneNumber');
  validateField('address');
  validateField('projectId');

  const activeErrors = Object.entries(errors.value).filter(([_, err]) => !!err);
  if (activeErrors.length > 0) {
    const firstError = activeErrors[0][1];
    toast.add({
      severity: 'warn',
      summary: t('clients.validation.formInvalid') || 'Datos inválidos',
      detail: firstError,
      life: 4000
    });
    return;
  }

  const fullName = (formData.value.fullName || '').trim();
  const email = (formData.value.email || '').trim();
  const phoneNumber = (formData.value.phoneNumber || '').trim();
  const address = (formData.value.address || '').trim();

  const clientPayload = new Client({
    fullName: fullName,
    email: email,
    phoneNumber: phoneNumber,
    address: address,
    projectId: formData.value.projectId,
    projectName: formData.value.projectName,
    accountStatement: formData.value.accountStatement || 'Active',
    builderId: formData.value.builderId || iamStore.currentUser?.id || 0,
    unitId: formData.value.unitId,
    unitNumber: formData.value.unitNumber
  });

  emit('save', clientPayload);
  localVisible.value = false;
};

const handleCancel = () => {
  errors.value = {};
  localVisible.value = false;
};
</script>

<template>
  <pv-dialog
    v-model:visible="localVisible"
    modal
    :header="t('clients.addDialogTitle')"
    :style="{ width: '600px' }"
    class="client-add-dialog"
  >
    <pv-toast />
    <div class="grid">
      <!-- General Form Alert Banner when errors exist -->
      <div v-if="Object.values(errors).some(e => !!e) && (touched.fullName || touched.email || touched.phoneNumber || touched.address || touched.projectId)" class="col-12 mb-2">
        <div class="field-alert field-alert--error p-3 font-medium">
          <i class="pi pi-exclamation-triangle field-alert__icon text-lg"></i>
          <span class="field-alert__text">
            {{ t('clients.validation.formInvalid') || 'Por favor revise y corrija los campos marcados antes de continuar.' }}
          </span>
        </div>
      </div>

      <!-- Full Name Field -->
      <div class="col-12 mb-3">
        <label for="fullName" class="block mb-2 font-semibold">{{ t('clients.fields.fullName') }} *</label>
        <pv-input-text
          id="fullName"
          v-model="formData.fullName"
          class="w-full"
          :class="{ 'input-invalid-custom': !!errors.fullName }"
          :invalid="!!errors.fullName"
          :placeholder="t('clients.placeholders.fullName')"
          @update:modelValue="(val) => onFieldInput('fullName', val)"
          @blur="onFieldBlur('fullName')"
        />
        <div v-if="errors.fullName" class="field-alert field-alert--error mt-1" role="alert">
          <i class="pi pi-exclamation-circle field-alert__icon"></i>
          <span class="field-alert__text">{{ errors.fullName }}</span>
        </div>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.fullName') }}
        </small>
      </div>

      <!-- Email Field -->
      <div class="col-12 mb-3">
        <label for="email" class="block mb-2 font-semibold">{{ t('clients.fields.email') }} *</label>
        <pv-input-text
          id="email"
          v-model="formData.email"
          class="w-full"
          :class="{ 'input-invalid-custom': !!errors.email }"
          :invalid="!!errors.email"
          :placeholder="t('clients.placeholders.email')"
          @update:modelValue="(val) => onFieldInput('email', val)"
          @blur="onFieldBlur('email')"
        />
        <div v-if="errors.email" class="field-alert field-alert--error mt-1" role="alert">
          <i class="pi pi-exclamation-circle field-alert__icon"></i>
          <span class="field-alert__text">{{ errors.email }}</span>
        </div>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.email') }}
        </small>
      </div>

      <!-- Phone Number Field -->
      <div class="col-12 mb-3">
        <label for="phoneNumber" class="block mb-2 font-semibold">{{ t('clients.fields.phoneNumber') }}</label>
        <pv-input-text
          id="phoneNumber"
          v-model="formData.phoneNumber"
          class="w-full"
          :class="{ 'input-invalid-custom': !!errors.phoneNumber }"
          :invalid="!!errors.phoneNumber"
          :placeholder="t('clients.placeholders.phoneNumber')"
          @update:modelValue="(val) => onFieldInput('phoneNumber', val)"
          @blur="onFieldBlur('phoneNumber')"
        />
        <div v-if="errors.phoneNumber" class="field-alert field-alert--error mt-1" role="alert">
          <i class="pi pi-exclamation-circle field-alert__icon"></i>
          <span class="field-alert__text">{{ errors.phoneNumber }}</span>
        </div>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.phoneNumber') }}
        </small>
      </div>

      <!-- Address Field -->
      <div class="col-12 mb-3">
        <label for="address" class="block mb-2 font-semibold">{{ t('clients.fields.address') }}</label>
        <pv-input-text
          id="address"
          v-model="formData.address"
          class="w-full"
          :class="{ 'input-invalid-custom': !!errors.address }"
          :invalid="!!errors.address"
          :placeholder="t('clients.placeholders.address')"
          @update:modelValue="(val) => onFieldInput('address', val)"
          @blur="onFieldBlur('address')"
        />
        <div v-if="errors.address" class="field-alert field-alert--error mt-1" role="alert">
          <i class="pi pi-exclamation-circle field-alert__icon"></i>
          <span class="field-alert__text">{{ errors.address }}</span>
        </div>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.address') }}
        </small>
      </div>

      <!-- Project Selection -->
      <div class="col-12 mb-3">
        <label for="projectId" class="block mb-2 font-semibold">{{ t('clients.fields.project') }} *</label>
        <pv-select
          id="projectId"
          v-model="formData.projectId"
          :options="projectOptions"
          optionLabel="label"
          optionValue="value"
          :placeholder="t('clients.placeholders.project')"
          class="w-full"
          :class="{ 'input-invalid-custom': !!errors.projectId }"
          :invalid="!!errors.projectId"
          :disabled="projectOptions.length === 0"
          @change="(e) => onFieldInput('projectId', e.value)"
          @update:modelValue="(val) => onFieldInput('projectId', val)"
          @blur="onFieldBlur('projectId')"
        />
        <div v-if="errors.projectId" class="field-alert field-alert--error mt-1" role="alert">
          <i class="pi pi-exclamation-circle field-alert__icon"></i>
          <span class="field-alert__text">{{ errors.projectId }}</span>
        </div>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.project') }}
        </small>
      </div>

      <!-- Unit Selection (Optional) -->
      <div class="col-12 mb-3">
        <label for="unitId" class="block mb-2 font-semibold">{{ t('clients.fields.assignedUnit') }}</label>
        <pv-select
          id="unitId"
          v-model="formData.unitId"
          :options="unitOptions"
          optionLabel="label"
          optionValue="value"
          :placeholder="t('clients.placeholders.unitOptional')"
          class="w-full"
          :loading="loadingUnits"
          :disabled="!formData.projectId || unitOptions.length <= 1"
        />
        <small v-if="formData.projectId && unitOptions.length <= 1 && !loadingUnits" class="text-gray-500 block mt-1">
          {{ t('clients.messages.noUnitsConfigured') }}
        </small>
        <small v-else-if="formData.projectId" class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.unit') }}
        </small>
      </div>
    </div>

    <template #footer>
      <pv-button
        :label="t('clients.actions.cancel')"
        icon="pi pi-times"
        @click="handleCancel"
        severity="danger"
        outlined
      />
      <pv-button
        :label="t('clients.actions.add')"
        icon="pi pi-check"
        @click="handleSave"
        severity="success"
      />
    </template>
  </pv-dialog>
</template>

<style scoped>
/* Forzar fondo blanco en el diálogo principal */
:deep(.p-dialog) {
  background: white !important;
}

:deep(.p-dialog .p-dialog-header) {
  background: white !important;
  color: #111827 !important;
}

:deep(.p-dialog .p-dialog-content) {
  background: white !important;
  color: #111827 !important;
}

:deep(.p-dialog .p-dialog-footer) {
  background: white !important;
}

/* Estilos para inputs */
:deep(.p-inputtext) {
  background: white !important;
  color: #111827 !important;
  border-color: #d1d5db;
}

:deep(.p-inputtext:enabled:hover) {
  background: white !important;
  border-color: #9ca3af;
}

:deep(.p-inputtext:enabled:focus) {
  background: white !important;
  border-color: #3b82f6 !important;
  box-shadow: 0 0 0 0.2rem rgba(59, 130, 246, 0.25) !important;
}

/* Invalid inputs styling */
:deep(.input-invalid-custom),
:deep(.p-inputtext.p-invalid),
:deep(.p-inputtext.input-invalid-custom),
:deep(.p-select.p-invalid),
:deep(.p-select.input-invalid-custom) {
  border-color: #ef4444 !important;
  background-color: #fef2f2 !important;
  box-shadow: 0 0 0 1px #ef4444 !important;
}

/* Alert boxes */
.field-alert {
  display: flex;
  align-items: flex-start;
  gap: 0.45rem;
  padding: 0.45rem 0.65rem;
  border-radius: 6px;
  margin-top: 0.35rem;
  font-size: 0.8125rem;
  line-height: 1.25rem;
}

.field-alert--error {
  background-color: #fef2f2 !important;
  border: 1px solid #fca5a5 !important;
  color: #991b1b !important;
}

.field-alert__icon {
  color: #dc2626 !important;
  font-size: 0.95rem !important;
  margin-top: 0.15rem;
  flex-shrink: 0;
}

.field-alert__text {
  color: #991b1b !important;
  font-weight: 500;
}

/* Estilos para el select */
:deep(.p-select) {
  background: white !important;
  color: #111827 !important;
  border-color: #d1d5db !important;
}

:deep(.p-select:hover) {
  background: white !important;
  border-color: #9ca3af !important;
}

:deep(.p-select:focus) {
  background: white !important;
  border-color: #3b82f6 !important;
  box-shadow: 0 0 0 0.2rem rgba(59, 130, 246, 0.25) !important;
}

:deep(.p-select .p-select-label) {
  background: white !important;
  color: #111827 !important;
}

:deep(.p-select .p-select-dropdown) {
  background: white !important;
  color: #111827 !important;
}

:deep(label) {
  color: #374151 !important;
}

/* Grid del formulario */
:deep(.grid) {
  background: white !important;
}

/* Estilos para los botones del footer */
:deep(.p-button) {
  color: white !important;
}

:deep(.p-button.p-button-danger.p-button-outlined) {
  background: #fee2e2 !important;
  border-color: #ef4444 !important;
  color: #dc2626 !important;
}

:deep(.p-button.p-button-danger.p-button-outlined:hover) {
  background: #fecaca !important;
  border-color: #dc2626 !important;
  color: #991b1b !important;
}

:deep(.p-button-success) {
  background: #10b981 !important;
  border-color: #10b981 !important;
  color: white !important;
}

:deep(.p-button-success:hover) {
  background: #059669 !important;
  border-color: #059669 !important;
  color: white !important;
}
</style>
