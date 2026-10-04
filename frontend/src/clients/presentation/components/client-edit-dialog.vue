<script setup>
import { ref, watch, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import { ProjectsFacade } from '../../infrastructure/projects.facade.js';
import {
  isValidEmail,
  isValidPhone,
  validateClientFullName,
  validateClientAddress,
  validateClientEmail
} from '../../../shared/presentation/validators.js';

const props = defineProps({
  visible: {
    type: Boolean,
    required: true
  },
  client: {
    type: Object,
    default: null
  }
});

const emit = defineEmits(['update:visible', 'save']);

const { t } = useI18n();
const toast = useToast();

const projectsFacade = new ProjectsFacade();
const localVisible = ref(props.visible);

const defaultErrors = () => ({
  fullName: '',
  email: '',
  phoneNumber: '',
  address: '',
  projectId: ''
});
const errors = ref(defaultErrors());

const projects = ref([]);
const units = ref([]);
const loadingUnits = ref(false);

const formData = ref({
  id: null,
  fullName: '',
  email: '',
  phoneNumber: '',
  address: '',
  projectId: null,
  projectName: '',
  accountStatement: 'Active',
  unitId: null,
  unitNumber: ''
});

async function loadUnitsForProject(projectId) {
  if (!projectId) {
    units.value = [];
    return;
  }
  loadingUnits.value = true;
  try {
    units.value = await projectsFacade.getUnitsByProject(projectId);
  } catch (e) {
    console.error('Error fetching units:', e);
    units.value = [];
  } finally {
    loadingUnits.value = false;
  }
}

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
    const isThisClient = formData.value.unitId === u.id || (u.ownerEmail && formData.value.email && u.ownerEmail.toLowerCase() === formData.value.email.toLowerCase());
    const isOccupied = !!u.ownerEmail && !isThisClient;
    const assignedText = t('projects.structure.assigned-to-this-client');
    const occupiedText = t('projects.structure.occupied');
    const availableText = t('projects.structure.available');
    const statusText = isThisClient ? `(${assignedText})` : (isOccupied ? `(${occupiedText} - ${u.ownerEmail})` : `(${availableText})`);
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

watch(() => props.visible, async (newVal) => {
  localVisible.value = newVal;
  errors.value = defaultErrors();
  touched.value = {
    fullName: false,
    email: false,
    phoneNumber: false,
    address: false,
    projectId: false
  };
  if (newVal && props.client) {
    formData.value = { ...props.client };
    if (props.client.projectId) {
      await loadUnitsForProject(props.client.projectId);
    }
  }
});

watch(localVisible, (newVal) => {
  emit('update:visible', newVal);
});

// Watch for project selection to update projectName
watch(() => formData.value.projectId, async (newProjectId, oldProjectId) => {
  if (newProjectId) {
    formData.value.projectName = projectsFacade.getProjectNameById(newProjectId);
    if (oldProjectId && newProjectId !== oldProjectId) {
      formData.value.unitId = null;
      formData.value.unitNumber = '';
      await loadUnitsForProject(newProjectId);
    }
  } else {
    formData.value.projectName = '';
    formData.value.unitId = null;
    formData.value.unitNumber = '';
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
  const val = formData.value ? formData.value[field] : '';
  if (field === 'fullName') {
    const res = validateClientFullName(val, t);
    errors.value.fullName = res.isValid ? '' : res.error;
  } else if (field === 'email') {
    const clean = (val || '').trim();
    if (!clean) {
      errors.value.email = '';
    } else {
      const res = validateClientEmail(clean, t);
      errors.value.email = res.isValid ? '' : res.error;
    }
  } else if (field === 'phoneNumber') {
    const clean = (val || '').trim();
    if (clean && !isValidPhone(clean)) {
      errors.value.phoneNumber = t('clients.validation.phoneInvalid');
    } else {
      errors.value.phoneNumber = '';
    }
  } else if (field === 'address') {
    const res = validateClientAddress(val, t);
    errors.value.address = res.isValid ? '' : res.error;
  } else if (field === 'projectId') {
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
  validateField(field);
};

const onFieldBlur = (field) => {
  touched.value[field] = true;
  validateField(field);
};

// Reactive watchers so errors appear and clear dynamically
watch(() => formData.value.email, () => {
  validateField('email');
});

watch(() => formData.value.fullName, () => {
  if (touched.value.fullName || (formData.value.fullName && formData.value.fullName.length > 0)) {
    validateField('fullName');
  }
});

watch(() => formData.value.phoneNumber, () => {
  validateField('phoneNumber');
});

watch(() => formData.value.address, () => {
  if (touched.value.address || (formData.value.address && formData.value.address.length > 0)) {
    validateField('address');
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

  validateField('fullName');

  const emailClean = (formData.value.email || '').trim();
  if (!emailClean) {
    errors.value.email = t('clients.validation.emailRequired');
  } else {
    const emailRes = validateClientEmail(emailClean, t);
    errors.value.email = emailRes.isValid ? '' : emailRes.error;
  }

  validateField('phoneNumber');
  validateField('address');
  validateField('projectId');

  const hasErrors = Object.values(errors.value).some(err => !!err);
  if (hasErrors) {
    const firstError = Object.values(errors.value).find(err => !!err);
    toast.add({
      severity: 'warn',
      summary: t('clients.validation.formInvalid') || 'Datos requeridos',
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

  emit('save', formData.value);
  localVisible.value = false;
};

const handleCancel = () => {
  errors.value = defaultErrors();
  localVisible.value = false;
};

const accountStatementOptions = computed(() => [
  { label: t('clients.status.active'), value: 'Active' },
  { label: t('clients.status.standBy'), value: 'Stand by' },
  { label: t('clients.status.suspended'), value: 'Suspended' }
]);
</script>

<template>
  <pv-dialog
    v-model:visible="localVisible"
    modal
    :header="t('clients.actions.editClient')"
    :style="{ width: '640px', maxWidth: '95vw' }"
    class="client-dialog client-edit-dialog"
  >
    <pv-toast />
    <div class="grid p-fluid">
      <!-- Section 1: Personal Information -->
      <div class="col-12 mb-2">
        <div class="flex items-center gap-2 pb-1 border-b border-gray-200 text-xs font-semibold text-gray-500 uppercase tracking-wider">
          <i class="pi pi-user text-primary text-xs"></i>
          <span>{{ t('clients.profile.personalInfo') }}</span>
        </div>
      </div>

      <!-- Full Name Field -->
      <div class="col-12 mb-3">
        <label for="fullName" class="block mb-1 text-sm font-semibold text-gray-700">
          {{ t('clients.fields.fullName') }} <span class="text-red-500">*</span>
        </label>
        <pv-input-text
          id="fullName"
          v-model="formData.fullName"
          class="w-full"
          :invalid="!!errors.fullName"
          :placeholder="t('clients.placeholders.fullName')"
          @input="(e) => onFieldInput('fullName', e?.target?.value)"
          @blur="onFieldBlur('fullName')"
        />
        <small v-if="errors.fullName" class="p-error block mt-1 text-xs">{{ errors.fullName }}</small>
        <small v-else class="text-xs text-gray-400 block mt-1">{{ t('clients.hints.fullName') }}</small>
      </div>

      <!-- Email Field -->
      <div class="col-12 mb-3">
        <label for="email" class="block mb-1 text-sm font-semibold text-gray-700">
          {{ t('clients.fields.email') }} <span class="text-red-500">*</span>
        </label>
        <pv-input-text
          id="email"
          v-model="formData.email"
          class="w-full"
          :invalid="!!errors.email"
          :placeholder="t('clients.placeholders.email')"
          @input="(e) => onFieldInput('email', e?.target?.value)"
          @blur="onFieldBlur('email')"
        />
        <small v-if="errors.email" class="p-error block mt-1 text-xs">{{ errors.email }}</small>
        <small v-else class="text-xs text-gray-400 block mt-1">{{ t('clients.hints.email') }}</small>
      </div>

      <!-- Phone Number Field -->
      <div class="col-12 md:col-6 mb-3">
        <label for="phoneNumber" class="block mb-1 text-sm font-semibold text-gray-700">
          {{ t('clients.fields.phoneNumber') }}
        </label>
        <pv-input-text
          id="phoneNumber"
          v-model="formData.phoneNumber"
          class="w-full"
          :invalid="!!errors.phoneNumber"
          :placeholder="t('clients.placeholders.phoneNumber')"
          @input="(e) => onFieldInput('phoneNumber', e?.target?.value)"
          @blur="onFieldBlur('phoneNumber')"
        />
        <small v-if="errors.phoneNumber" class="p-error block mt-1 text-xs">{{ errors.phoneNumber }}</small>
        <small v-else class="text-xs text-gray-400 block mt-1">{{ t('clients.hints.phoneNumber') }}</small>
      </div>

      <!-- Address Field -->
      <div class="col-12 md:col-6 mb-3">
        <label for="address" class="block mb-1 text-sm font-semibold text-gray-700">
          {{ t('clients.fields.address') }}
        </label>
        <pv-input-text
          id="address"
          v-model="formData.address"
          class="w-full"
          :invalid="!!errors.address"
          :placeholder="t('clients.placeholders.address')"
          @input="(e) => onFieldInput('address', e?.target?.value)"
          @blur="onFieldBlur('address')"
        />
        <small v-if="errors.address" class="p-error block mt-1 text-xs">{{ errors.address }}</small>
        <small v-else class="text-xs text-gray-400 block mt-1">{{ t('clients.hints.address') }}</small>
      </div>

      <!-- Section 2: Project & Status Assignment -->
      <div class="col-12 mt-2 mb-2">
        <div class="flex items-center gap-2 pb-1 border-b border-gray-200 text-xs font-semibold text-gray-500 uppercase tracking-wider">
          <i class="pi pi-building text-primary text-xs"></i>
          <span>{{ t('clients.profile.projectInfo') }}</span>
        </div>
      </div>

      <!-- Project Selection -->
      <div class="col-12 md:col-6 mb-3">
        <label for="projectId" class="block mb-1 text-sm font-semibold text-gray-700">
          {{ t('clients.fields.project') }} <span class="text-red-500">*</span>
        </label>
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
          @change="onFieldInput('projectId')"
          @blur="onFieldBlur('projectId')"
        />
        <small v-if="errors.projectId" class="p-error block mt-1 text-xs">{{ errors.projectId }}</small>
        <small v-else class="text-xs text-gray-400 block mt-1">{{ t('clients.hints.project') }}</small>
      </div>

      <!-- Unit Selection (Optional) -->
      <div class="col-12 md:col-6 mb-3">
        <label for="unitId" class="block mb-1 text-sm font-semibold text-gray-700">
          {{ t('clients.fields.assignedUnit') }}
        </label>
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
        <small v-if="formData.projectId && unitOptions.length <= 1 && !loadingUnits" class="text-xs text-gray-400 block mt-1">
          {{ t('clients.messages.noUnitsConfigured') }}
        </small>
        <small v-else-if="formData.projectId" class="text-xs text-gray-400 block mt-1">
          {{ t('clients.hints.unit') }}
        </small>
      </div>

      <!-- Account Statement -->
      <div class="col-12 mb-3">
        <label for="accountStatement" class="block mb-1 text-sm font-semibold text-gray-700">
          {{ t('clients.fields.accountStatement') }}
        </label>
        <pv-select
          id="accountStatement"
          v-model="formData.accountStatement"
          :options="accountStatementOptions"
          optionLabel="label"
          optionValue="value"
          class="w-full"
        />
      </div>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2 pt-2">
        <pv-button
          :label="t('clients.actions.cancel')"
          icon="pi pi-times"
          severity="secondary"
          text
          @click="handleCancel"
        />
        <pv-button
          :label="t('clients.actions.save')"
          icon="pi pi-check"
          severity="primary"
          @click="handleSave"
        />
      </div>
    </template>
  </pv-dialog>
</template>

<style scoped>
.p-error {
  color: #ef4444;
}
</style>
