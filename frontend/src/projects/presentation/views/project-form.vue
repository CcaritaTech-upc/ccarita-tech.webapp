<script setup>
import { ref, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { useToast } from "primevue/usetoast";
import useProjectStore from "../../application/project.store.js";
import { Project } from "../../domain/model/project.entity.js";
import useSubscriptionStore from "../../../subscriptions/application/subscription.store.js";
import { getPlanLimits } from "../../../subscriptions/domain/model/plan-limits.js";
import { CLOUDINARY_WIDGET_URL } from "../../../shared/infrastructure/constants.js";
import { getProjectImageUploadConfig } from "../../../shared/infrastructure/cloudinary-config.js";
import {
  validateProjectName,
  validateProjectLocation,
  validateProjectDescription,
  isValidUrl
} from "../../../shared/presentation/validators.js";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const store = useProjectStore();
const subscriptionStore = useSubscriptionStore();

const form = ref({
  name: "",
  description: "",
  location: "",
  totalUnits: 0,
  occupiedUnits: 0,
  imageUrl: ""
});
const errors = ref({});
const isEdit = computed(() => !!route.params.id);
const saving = ref(false);
const cloudinaryName = import.meta.env.VITE_CLOUDINARY_CLOUD_NAME;
const cloudinaryPreset = import.meta.env.VITE_CLOUDINARY_UPLOAD_PRESET;
const cloudinaryReady = ref(false);
const fileInput = ref(null);

const validateField = (field) => {
  if (field === 'name') {
    const res = validateProjectName(form.value.name, t);
    errors.value.name = res.isValid ? '' : res.error;
  } else if (field === 'location') {
    const res = validateProjectLocation(form.value.location, t);
    errors.value.location = res.isValid ? '' : res.error;
  } else if (field === 'description') {
    const res = validateProjectDescription(form.value.description, t);
    errors.value.description = res.isValid ? '' : res.error;
  }
};

onMounted(async () => {
  if (!subscriptionStore.currentPlan && !subscriptionStore.isLoading) {
    subscriptionStore.loadSubscriptions();
  }
  if (!store.projects.length) {
    store.fetchProjects();
  }

  if (isEdit.value) {
    let existing = store.getProjectById(route.params.id);
    if (!existing) {
      await store.fetchProjects();
      existing = store.getProjectById(route.params.id);
    }
    if (existing) {
      form.value = {
        name: existing.name || "",
        description: existing.description || "",
        location: existing.location || "",
        totalUnits: existing.totalUnits || 0,
        occupiedUnits: existing.occupiedUnits || 0,
        imageUrl: existing.imageUrl || ""
      };
    } else {
      router.push({ name: "projects-management" });
    }
  }

  loadCloudinaryScript();
});

const loadCloudinaryScript = () => {
  if (window.cloudinary) {
    cloudinaryReady.value = true;
    return;
  }
  const script = document.createElement('script');
  script.src = CLOUDINARY_WIDGET_URL;
  script.type = 'text/javascript';
  script.onload = () => { cloudinaryReady.value = true; };
  document.head.appendChild(script);
};

const handleLocalFileUpload = async (event) => {
  const file = event.target.files?.[0];
  if (!file) return;

  if (cloudinaryName && cloudinaryPreset) {
    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('upload_preset', cloudinaryPreset);
      const res = await fetch(`https://api.cloudinary.com/v1_1/${cloudinaryName}/image/upload`, {
        method: 'POST',
        body: formData,
      });
      const data = await res.json();
      if (data.secure_url) {
        form.value.imageUrl = data.secure_url;
        return;
      }
    } catch (e) {
      console.warn('Direct upload to Cloudinary failed:', e);
    }
  }

  const reader = new FileReader();
  reader.onload = (e) => {
    form.value.imageUrl = e.target.result;
  };
  reader.readAsDataURL(file);
};

const openUploadModal = () => {
  const hasCloudinary = cloudinaryName && cloudinaryName.trim() !== '' && cloudinaryPreset && cloudinaryPreset.trim() !== '';
  if (hasCloudinary && cloudinaryReady.value && window.cloudinary) {
    try {
      const widgetConfig = getProjectImageUploadConfig(cloudinaryName, cloudinaryPreset);
      window.cloudinary.openUploadWidget(
        widgetConfig,
        (error, result) => {
          if (!error && result && result.event === "success") {
            form.value.imageUrl = result.info.secure_url || result.info.url;
          }
        }
      );
      return;
    } catch (err) {
      console.warn('Cloudinary widget failed, using local file picker:', err);
    }
  }

  if (fileInput.value) {
    fileInput.value.click();
  }
};

const save = async () => {
  errors.value = {};

  const name = (form.value.name || '').trim();
  const location = (form.value.location || '').trim();
  const description = (form.value.description || '').trim();
  const imageUrl = (form.value.imageUrl || '').trim();

  validateField('name');
  validateField('location');
  validateField('description');

  if (imageUrl && !imageUrl.startsWith('data:') && !isValidUrl(imageUrl)) {
    errors.value.imageUrl = t('projects.validation.imageInvalid') || 'Ingrese una URL de imagen válida.';
  }

  const activeErrors = Object.entries(errors.value).filter(([_, err]) => !!err);
  if (activeErrors.length > 0) {
    const firstError = activeErrors[0][1];
    toast.add({
      severity: 'warn',
      summary: t('common.warning') || 'Datos inválidos',
      detail: firstError,
      life: 4000
    });
    return;
  }

  if (!isEdit.value) {
    const limits = getPlanLimits(subscriptionStore.currentPlan);
    if (!limits.isUnlimited && store.projects.length >= limits.maxProjects) {
      toast.add({
        severity: 'warn',
        summary: t('subscriptions.warning') || 'Límite de proyectos alcanzado',
        detail: t('subscriptions.projectLimitExceeded', { max: limits.maxProjects }) || `Has alcanzado el límite de proyectos de tu plan (${limits.maxProjects} proyectos). Actualiza tu suscripción para crear más proyectos.`,
        life: 4000
      });
      return;
    }
  }

  saving.value = true;
  form.value.name = name;
  form.value.location = location;
  form.value.description = description;

  try {
    const project = new Project({
      id: isEdit.value ? parseInt(route.params.id) : null,
      ...form.value,
    });
    if (isEdit.value) {
      await store.updateProject(project);
      toast.add({
        severity: 'success',
        summary: t('common.success') || 'Éxito',
        detail: t('projects.messages.saved') || 'Proyecto actualizado exitosamente',
        life: 3000
      });
      router.push({ name: "projects-management-details", params: { id: route.params.id } });
    } else {
      const created = await store.addProject(project);
      toast.add({
        severity: 'success',
        summary: t('common.success') || 'Éxito',
        detail: t('projects.messages.saved') || 'Proyecto creado exitosamente',
        life: 3000
      });
      const newId = created?.id;
      if (newId) {
        router.push({
          name: "projects-management-details",
          params: { id: newId },
          query: { openStructure: "true" }
        });
      } else {
        router.push({ name: "projects-management" });
      }
    }
  } catch (error) {
    console.error('Error saving project:', error);
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: 'No se pudo guardar el proyecto. Por favor verifique los datos.',
      life: 4000
    });
  } finally {
    saving.value = false;
  }
};

const cancel = () => {
  if (isEdit.value) {
    router.push({ name: "projects-management-details", params: { id: route.params.id } });
  } else {
    router.push({ name: "projects-management" });
  }
};
</script>


<template>
  <div class="p-4 bg-gray-50 min-h-screen">
    <div class="form-container">
      <!-- Header -->
      <div class="mb-6">
        <div class="flex align-items-center gap-2 text-sm text-gray-600 mb-2">
          <i class="pi pi-home"></i>
          <span>/</span>
          <span class="breadcrumb-active font-medium">{{ isEdit ? t("projects.edit-title") : t("projects.new-title") }}</span>
        </div>
        <h1 class="text-2xl font-bold text-gray-900">
          {{ isEdit ? t("projects.edit-title") : t("projects.new-title") }}
        </h1>
      </div>

      <!-- Form Card -->
      <div class="form-card p-6">
        <form @submit.prevent="save">
          <!-- Name Field -->
          <div class="form-field mb-4">
            <label class="form-label">
              <i class="pi pi-tag form-label__icon mr-2"></i>
              {{ t("projects.fields.name") }}
              <span class="text-red-500 ml-1">*</span>
            </label>
            <pv-input-text
                v-model="form.name"
                class="w-full input-enhanced"
                :invalid="!!errors.name"
                :placeholder="t('projects.fields.name-placeholder')"
                @blur="validateField('name')"
                @input="errors.name && validateField('name')"
            />
            <small v-if="errors.name" class="p-error block mt-1">{{ errors.name }}</small>
          </div>

          <!-- Description Field -->
          <div class="form-field mb-4">
            <label class="form-label">
              <i class="pi pi-align-left form-label__icon mr-2"></i>
              {{ t("projects.fields.description") }}
            </label>
            <pv-textarea
                v-model="form.description"
                class="w-full input-enhanced"
                :invalid="!!errors.description"
                rows="3"
                :placeholder="t('projects.fields.description-placeholder')"
                @blur="validateField('description')"
                @input="errors.description && validateField('description')"
            />
            <small v-if="errors.description" class="p-error block mt-1">{{ errors.description }}</small>
          </div>

          <!-- Location Field -->
          <div class="form-field mb-4">
            <label class="form-label">
              <i class="pi pi-map-marker form-label__icon mr-2"></i>
              {{ t("projects.fields.location") }}
              <span class="text-red-500 ml-1">*</span>
            </label>
            <pv-input-text
                v-model="form.location"
                class="w-full input-enhanced"
                :invalid="!!errors.location"
                :placeholder="t('projects.fields.location-placeholder')"
                @blur="validateField('location')"
                @input="errors.location && validateField('location')"
            />
            <small v-if="errors.location" class="p-error block mt-1">{{ errors.location }}</small>
          </div>

          <!-- Total Units — read-only on edit; hidden on creation because
               define-structure always overwrites it with floors × unitsPerFloor. -->
          <div v-if="isEdit" class="form-field mb-4">
            <label class="form-label">
              <i class="pi pi-building form-label__icon mr-2"></i>
              {{ t("projects.fields.total-units") }}
            </label>
            <pv-input-number
                v-model="form.totalUnits"
                :min="0"
                class="w-full input-enhanced"
                placeholder="0"
                :disabled="true"
            />
            <small class="field-hint">
              <i class="pi pi-lock"></i>
              Set by the project structure — edit floors and units from Define Structure.
            </small>
          </div>

          <!-- Image URL Field -->
          <div class="form-field mb-4">
            <label class="form-label">
              <i class="pi pi-image form-label__icon mr-2"></i>
              {{ t("projects.fields.image-url") }}
            </label>
            <input
              type="file"
              ref="fileInput"
              accept="image/*"
              style="display: none;"
              @change="handleLocalFileUpload"
            />
            <pv-button
                :label="form.imageUrl ? 'Cambiar Imagen' : t('projects.actions.upload-image')"
                icon="pi pi-cloud-upload"
                @click="openUploadModal"
                class="mb-3"
            />
            <div v-if="form.imageUrl" class="mt-3">
              <img :src="form.imageUrl" alt="Uploaded image" style="max-width: 400px; border-radius: 8px;" />
              <div class="mt-2">
                <pv-button
                  type="button"
                  label="Eliminar imagen"
                  icon="pi pi-trash"
                  text
                  severity="danger"
                  size="small"
                  @click="form.imageUrl = ''"
                />
              </div>
            </div>
          </div>

          <!-- Action Buttons -->
          <div class="flex gap-3 pt-4 border-t border-gray-200">
            <pv-button
                type="submit"
                :label="isEdit ? t('projects.actions.save') : (t('projects.actions.save-and-configure') || 'Guardar y Configurar Estructura')"
                icon="pi pi-check"
                :loading="saving"
                class="custom-green-button flex-1"
            />
            <pv-button
                :label="t('projects.actions.cancel')"
                icon="pi pi-times"
                severity="secondary"
                class="cancel-button flex-1"
                @click="cancel"
            />
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* Centered container with max-width, matching max-w-3xl (48rem) */
.form-container {
  max-width: 48rem;
  margin: 0 auto;
}

/* Form card: white bg, rounded corners, subtle shadow and border */
.form-card {
  background: #ffffff;
  border-radius: 0.5rem;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
  border: 1px solid #e5e7eb;
}

/* Breadcrumb active item in brand green */
.breadcrumb-active {
  color: #059669;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-label {
  font-size: 0.875rem;
  font-weight: 600;
  color: #374151;
  display: flex;
  align-items: center;
}

.form-label__icon {
  color: #059669;
}

.field-hint {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  margin-top: 0.35rem;
  font-size: 0.75rem;
  color: #6b7280;
}

.field-hint .pi {
  font-size: 0.7rem;
  color: #9ca3af;
}

:deep(.input-enhanced input),
:deep(.input-enhanced textarea),
:deep(.input-enhanced .p-inputnumber-input) {
  border-color: #e5e7eb !important;
  transition: all 0.2s ease !important;
}

:deep(.input-enhanced input:focus),
:deep(.input-enhanced textarea:focus),
:deep(.input-enhanced .p-inputnumber-input:focus) {
  border-color: #10B981 !important;
  box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.1) !important;
}

:deep(.custom-green-button) {
  background-color: #10B981 !important;
  border-color: #10B981 !important;
  color: white !important;
  transition: all 0.2s ease !important;
  padding: 0.625rem 1.5rem !important;
  font-weight: 600 !important;
}

:deep(.custom-green-button:hover) {
  background-color: #059669 !important;
  border-color: #059669 !important;
  transform: translateY(-1px);
  box-shadow: 0 4px 6px -1px rgba(16, 185, 129, 0.3) !important;
}

:deep(.custom-green-button:focus) {
  box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.3) !important;
}

:deep(.cancel-button) {
  background-color: white !important;
  border-color: #e5e7eb !important;
  color: #6b7280 !important;
  transition: all 0.2s ease !important;
  padding: 0.625rem 1.5rem !important;
  font-weight: 600 !important;
}

:deep(.cancel-button:hover) {
  background-color: #f9fafb !important;
  border-color: #d1d5db !important;
  color: #374151 !important;
}
</style>
