<script setup>
import { ref, watch, computed, onMounted } from 'vue';
import { Client } from '../../domain/model/client.entity.js';
import { ProjectsFacade } from '../../infrastructure/projects.facade.js';
import { isValidEmail, isValidPhone, isValidName } from '../../../shared/presentation/validators.js';
import { useIamStore } from '../../../iam/application/iam.store.js';

const props = defineProps({
  visible: {
    type: Boolean,
    required: true
  }
});

const emit = defineEmits(['update:visible', 'save']);

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
  const list = [
    { label: 'Sin asignar / None', value: null }
  ];
  units.value.forEach(u => {
    const isOccupied = !!u.ownerEmail;
    const statusText = isOccupied ? `(Ocupada - ${u.ownerEmail})` : '(Disponible)';
    list.push({
      label: `Unidad ${u.unitNumber || u.roomNumber} - Piso ${u.floor} ${statusText}`,
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

const handleSave = () => {
  errors.value = {};

  const fullName = (formData.value.fullName || '').trim();
  const email = (formData.value.email || '').trim();
  const phoneNumber = (formData.value.phoneNumber || '').trim();
  const address = (formData.value.address || '').trim();
  const projectId = formData.value.projectId;

  if (!fullName) {
    errors.value.fullName = 'El nombre completo es obligatorio.';
  } else if (!isValidName(fullName, 2)) {
    errors.value.fullName = 'El nombre completo debe tener al menos 2 caracteres.';
  }

  if (!email) {
    errors.value.email = 'El correo electrónico es obligatorio.';
  } else if (!isValidEmail(email)) {
    errors.value.email = 'Ingrese un correo electrónico válido (ejemplo: usuario@empresa.com).';
  }

  if (phoneNumber && !isValidPhone(phoneNumber)) {
    errors.value.phoneNumber = 'Ingrese un número telefónico válido (de 7 a 15 dígitos numéricos).';
  }

  if (!projectId) {
    errors.value.projectId = 'Debe seleccionar un proyecto para este cliente.';
  }

  if (Object.keys(errors.value).length > 0) {
    return;
  }

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
    header="Add New Client"
    :style="{ width: '600px' }"
    class="client-add-dialog"
  >
    <div class="grid">
      <div class="col-12 mb-3">
        <label for="fullName" class="block mb-2 font-semibold">Full Name *</label>
        <pv-input-text
          id="fullName"
          v-model="formData.fullName"
          class="w-full"
          :invalid="!!errors.fullName"
          placeholder="Enter full name"
        />
        <small v-if="errors.fullName" class="p-error block mt-1">{{ errors.fullName }}</small>
      </div>

      <div class="col-12 mb-3">
        <label for="email" class="block mb-2 font-semibold">Email *</label>
        <pv-input-text
          id="email"
          v-model="formData.email"
          class="w-full"
          type="email"
          :invalid="!!errors.email"
          placeholder="Enter email address"
        />
        <small v-if="errors.email" class="p-error block mt-1">{{ errors.email }}</small>
      </div>

      <div class="col-12 mb-3">
        <label for="phoneNumber" class="block mb-2 font-semibold">Phone Number</label>
        <pv-input-text
          id="phoneNumber"
          v-model="formData.phoneNumber"
          class="w-full"
          :invalid="!!errors.phoneNumber"
          placeholder="Enter phone number"
        />
        <small v-if="errors.phoneNumber" class="p-error block mt-1">{{ errors.phoneNumber }}</small>
      </div>

      <div class="col-12 mb-3">
        <label for="address" class="block mb-2 font-semibold">Address</label>
        <pv-input-text
          id="address"
          v-model="formData.address"
          class="w-full"
          placeholder="Enter address"
        />
      </div>

      <div class="col-12 mb-3">
        <label for="projectId" class="block mb-2 font-semibold">Project *</label>
        <pv-select
          id="projectId"
          v-model="formData.projectId"
          :options="projectOptions"
          optionLabel="label"
          optionValue="value"
          placeholder="Select a project"
          class="w-full"
          :invalid="!!errors.projectId"
          :disabled="projectOptions.length === 0"
        />
        <small v-if="errors.projectId" class="p-error block mt-1">{{ errors.projectId }}</small>
      </div>

      <div class="col-12 mb-3">
        <label for="unitId" class="block mb-2 font-semibold">Unidad / Departamento asignado</label>
        <pv-select
          id="unitId"
          v-model="formData.unitId"
          :options="unitOptions"
          optionLabel="label"
          optionValue="value"
          placeholder="Seleccionar unidad (opcional)"
          class="w-full"
          :loading="loadingUnits"
          :disabled="!formData.projectId || unitOptions.length <= 1"
        />
        <small v-if="formData.projectId && unitOptions.length <= 1 && !loadingUnits" class="text-gray-500 block mt-1">
          Este proyecto no tiene unidades configuradas todavía.
        </small>
      </div>
    </div>

    <template #footer>
      <pv-button
        label="Cancel"
        icon="pi pi-times"
        @click="handleCancel"
        severity="danger"
        outlined
      />
      <pv-button
        label="Add Client"
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
