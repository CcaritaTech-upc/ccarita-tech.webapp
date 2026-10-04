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
const formData = ref(new Client({
  fullName: '',
  email: '',
  phoneNumber: '',
  address: '',
  projectName: '',
  accountStatement: 'Active',
  builderId: iamStore.currentUser?.id || 0,
  unitId: null,
  unitNumber: ''
}));

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

watch(() => props.visible, (newVal) => {
  localVisible.value = newVal;
  errors.value = {};
  if (newVal) {
    // Reset form when dialog opens
    formData.value = new Client({
      fullName: '',
      email: '',
      phoneNumber: '',
      address: '',
      projectId: 0,
      projectName: '',
      accountStatement: 'Active',
      builderId: iamStore.currentUser?.id || 0,
      unitId: null,
      unitNumber: ''
    });
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

const validateField = (field) => {
  if (field === 'fullName') {
    const res = validateClientFullName(formData.value.fullName, t);
    errors.value.fullName = res.isValid ? '' : res.error;
  } else if (field === 'email') {
    const val = (formData.value.email || '').trim();
    if (!val) {
      errors.value.email = t('clients.validation.emailRequired');
    } else if (!isValidEmail(val)) {
      errors.value.email = t('clients.validation.emailInvalid');
    } else {
      errors.value.email = '';
    }
  } else if (field === 'phoneNumber') {
    const val = (formData.value.phoneNumber || '').trim();
    if (val && !isValidPhone(val)) {
      errors.value.phoneNumber = t('clients.validation.phoneInvalid');
    } else {
      errors.value.phoneNumber = '';
    }
  } else if (field === 'address') {
    const res = validateClientAddress(formData.value.address, t);
    errors.value.address = res.isValid ? '' : res.error;
  } else if (field === 'projectId') {
    if (!formData.value.projectId) {
      errors.value.projectId = t('clients.validation.projectRequired');
    } else {
      errors.value.projectId = '';
    }
  }
};

const handleSave = () => {
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

  formData.value.fullName = fullName;
  formData.value.email = email;
  formData.value.phoneNumber = phoneNumber;
  formData.value.address = address;
  formData.value.builderId = formData.value.builderId || iamStore.currentUser?.id || 0;

  emit('save', formData.value);
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
    <div class="grid">
      <div class="col-12 mb-3">
        <label for="fullName" class="block mb-2 font-semibold">{{ t('clients.fields.fullName') }} *</label>
        <pv-input-text
          id="fullName"
          v-model="formData.fullName"
          class="w-full"
          :invalid="!!errors.fullName"
          :placeholder="t('clients.placeholders.fullName')"
          @blur="validateField('fullName')"
          @input="validateField('fullName')"
        />
        <small v-if="errors.fullName" class="p-error block mt-1">
          <i class="pi pi-exclamation-triangle mr-1"></i>{{ errors.fullName }}
        </small>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.fullName') }}
        </small>
      </div>

      <div class="col-12 mb-3">
        <label for="email" class="block mb-2 font-semibold">{{ t('clients.fields.email') }} *</label>
        <pv-input-text
          id="email"
          v-model="formData.email"
          class="w-full"
          type="email"
          :invalid="!!errors.email"
          :placeholder="t('clients.placeholders.email')"
          @blur="validateField('email')"
          @input="validateField('email')"
        />
        <small v-if="errors.email" class="p-error block mt-1">
          <i class="pi pi-exclamation-triangle mr-1"></i>{{ errors.email }}
        </small>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.email') }}
        </small>
      </div>

      <div class="col-12 mb-3">
        <label for="phoneNumber" class="block mb-2 font-semibold">{{ t('clients.fields.phoneNumber') }}</label>
        <pv-input-text
          id="phoneNumber"
          v-model="formData.phoneNumber"
          class="w-full"
          :invalid="!!errors.phoneNumber"
          :placeholder="t('clients.placeholders.phoneNumber')"
          @blur="validateField('phoneNumber')"
          @input="validateField('phoneNumber')"
        />
        <small v-if="errors.phoneNumber" class="p-error block mt-1">
          <i class="pi pi-exclamation-triangle mr-1"></i>{{ errors.phoneNumber }}
        </small>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.phoneNumber') }}
        </small>
      </div>

      <div class="col-12 mb-3">
        <label for="address" class="block mb-2 font-semibold">{{ t('clients.fields.address') }}</label>
        <pv-input-text
          id="address"
          v-model="formData.address"
          class="w-full"
          :invalid="!!errors.address"
          :placeholder="t('clients.placeholders.address')"
          @blur="validateField('address')"
          @input="validateField('address')"
        />
        <small v-if="errors.address" class="p-error block mt-1">
          <i class="pi pi-exclamation-triangle mr-1"></i>{{ errors.address }}
        </small>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.address') }}
        </small>
      </div>

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
          :invalid="!!errors.projectId"
          :disabled="projectOptions.length === 0"
          @change="validateField('projectId')"
        />
        <small v-if="errors.projectId" class="p-error block mt-1">
          <i class="pi pi-exclamation-triangle mr-1"></i>{{ errors.projectId }}
        </small>
        <small v-else class="text-xs text-gray-500 block mt-1">
          {{ t('clients.hints.project') }}
        </small>
      </div>

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
  border-color: #d1d5db !important;
}

:deep(.p-inputtext:enabled:hover) {
  background: white !important;
  border-color: #9ca3af !important;
}

:deep(.p-inputtext:enabled:focus) {
  background: white !important;
  border-color: #3b82f6 !important;
  box-shadow: 0 0 0 0.2rem rgba(59, 130, 246, 0.25) !important;
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
