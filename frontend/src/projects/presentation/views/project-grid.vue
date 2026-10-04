<script setup>
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import useProjectStore from "../../application/project.store.js";
import ProjectCard from "../components/project-card.vue";

const { t } = useI18n();
const router = useRouter();
const store = useProjectStore();
const loading = ref(false);

onMounted(async () => {
  loading.value = true;
  try {
    await store.fetchProjects();
  } finally {
    loading.value = false;
  }
});

const navigateToNew = () => router.push({ name: "projects-management-new" });
const navigateToDetails = (id) => router.push({ name: "projects-management-details", params: { id } });
</script>

<template>
  <div class="projects-view p-4">
    <!-- View Header -->
    <div class="flex align-items-center justify-content-between mb-4 flex-wrap gap-2">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 m-0">{{ t("projects.title") }}</h1>
        <p class="text-sm text-gray-500 mt-1 m-0">Gestiona tus proyectos, pisos, unidades y dispositivos IoT.</p>
      </div>
      <pv-button
        v-if="store.projects.length"
        :label="t('projects.add')"
        icon="pi pi-plus"
        class="custom-green-button"
        @click="navigateToNew"
      />
    </div>

    <!-- Loading State -->
    <div v-if="loading && !store.projects.length" class="flex justify-content-center align-items-center py-8">
      <i class="pi pi-spin pi-spinner text-3xl text-emerald-600"></i>
    </div>

    <!-- Project Cards Grid -->
    <div
      v-else-if="store.projects.length"
      class="project-grid"
    >
      <ProjectCard
        v-for="project in store.projects"
        :key="project.id"
        :project="project"
        @viewDetails="navigateToDetails(project.id)"
      />
    </div>

    <!-- Empty State -->
    <div v-else class="empty-state-box">
      <div class="empty-icon-wrap">
        <i class="pi pi-building"></i>
      </div>
      <h3 class="text-lg font-bold text-gray-800 m-0">{{ t('projects.messages.no-projects') }}</h3>
      <p class="text-sm text-gray-500 mt-1 mb-4">Aún no has registrado ningún proyecto. Comienza creando el primero.</p>
      <pv-button
        :label="t('projects.add')"
        icon="pi pi-plus"
        class="custom-green-button"
        @click="navigateToNew"
      />
    </div>
  </div>
</template>

<style scoped>
.projects-view {
  max-width: 1400px;
  margin: 0 auto;
}

/* Adaptive responsive grid: auto-fill with 290px minimum card width */
.project-grid {
  display: grid;
  gap: 1.5rem;
  grid-template-columns: repeat(auto-fill, minmax(290px, 1fr));
}

.empty-state-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 4rem 2rem;
  background: #ffffff;
  border-radius: 0.75rem;
  border: 1px dashed #d1d5db;
  margin-top: 1rem;
}

.empty-icon-wrap {
  width: 4rem;
  height: 4rem;
  border-radius: 50%;
  background: #ecfdf5;
  color: #10b981;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 1rem;
}

.empty-icon-wrap .pi {
  font-size: 1.75rem;
}

:deep(.custom-green-button) {
  background-color: #10b981 !important;
  border-color: #10b981 !important;
  color: white !important;
  font-weight: 600 !important;
  padding: 0.55rem 1.1rem !important;
  transition: all 0.2s ease !important;
}

:deep(.custom-green-button:hover) {
  background-color: #059669 !important;
  border-color: #059669 !important;
  transform: translateY(-1px);
  box-shadow: 0 4px 6px -1px rgba(16, 185, 129, 0.3) !important;
}

:deep(.custom-green-button:focus) {
  box-shadow: 0 0 0 0.2rem rgba(16, 185, 129, 0.4) !important;
}
</style>
