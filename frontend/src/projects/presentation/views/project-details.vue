<script setup>
import { onMounted, ref, computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { useConfirm } from "primevue/useconfirm";
import useProjectStore from "../../application/project.store.js";
import { useAnalyticsStore } from "../../../analytics/application/analytics.store.js";
import DefineStructureDialog from "../components/define-structure-dialog.vue";
import { ProjectApi } from "../../infrastructure/project-api.js";
import { ProjectAssembler } from "../../infrastructure/project.assembler.js";
import { ClientApi } from "../../../clients/infrastructure/client-api.js";

const { t, te } = useI18n();
const route = useRoute();
const router = useRouter();
const confirm = useConfirm();
const store = useProjectStore();
const analyticsStore = useAnalyticsStore();
const projectApi = new ProjectApi();
const clientApi = new ClientApi();
const project = ref(null);
const units = ref([]);
const projectClients = ref([]);
const unitsError = ref(null);
const showStructureDialog = ref(false);
const showAssignModal = ref(false);
const selectedUnitForAssign = ref(null);
const selectedClientId = ref(null);
const isAssigning = ref(false);
const savingUnit = ref(null);
// Falls back to a branded placeholder when the project image fails to load.
const imageBroken = ref(false);

async function loadUnits() {
  unitsError.value = null;
  try {
    const data = await projectApi.getUnitsByProject(route.params.id);
    units.value = Array.isArray(data) ? data : [];
  } catch (error) {
    console.error("Error loading units:", error);
    unitsError.value = error;
  }
}

async function loadProjectClients() {
  try {
    const res = await clientApi.getClientsByProjectId(route.params.id);
    projectClients.value = Array.isArray(res?.data) ? res.data : [];
  } catch (error) {
    console.error("Error loading project clients:", error);
  }
}

onMounted(async () => {
  await store.fetchProjects();
  project.value = store.getProjectById(route.params.id);
  if (!project.value) {
    try {
      const res = await projectApi.getProjectById(route.params.id);
      if (res?.data) {
        project.value = ProjectAssembler.toEntityFromResource(res.data);
      }
    } catch (e) {
      console.warn("Could not load project directly:", e);
    }
  }
  await Promise.all([loadUnits(), loadProjectClients()]);
  if (route.query.openStructure === 'true' && !hasStructure.value) {
    showStructureDialog.value = true;
  }
});

// A project has a structure once it actually has units or its structureDefined flag is set.
const hasStructure = computed(() => units.value.length > 0 || Boolean(project.value?.structureDefined));

// Group the project's units by floor for the structure display.
const structureGrid = computed(() => {
  const byFloor = new Map();
  for (const u of units.value) {
    if (!byFloor.has(u.floor)) byFloor.set(u.floor, []);
    byFloor.get(u.floor).push(u);
  }
  return [...byFloor.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([floor, list]) => ({
      floor,
      units: list.slice().sort((a, b) => (a.roomNumber || "").localeCompare(b.roomNumber || "")),
    }));
});

// How many units on a floor already have an owner assigned (for the floor header).
function occupiedCount(unitList) {
  return unitList.filter((u) => u.ownerEmail).length;
}

const currentOccupancyRate = computed(() => {
  const total = units.value.length > 0 ? units.value.length : (project.value?.totalUnits || 0);
  if (total <= 0) return 0;
  const occupied = units.value.length > 0 ? occupiedCount(units.value) : (project.value?.occupiedUnits || 0);
  return Math.min(100, Math.round((occupied / total) * 100));
});

// Human-readable created date (raw value is an ISO timestamp).
function formatDate(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

const navigateBack = () => router.push({ name: "projects-management" });
const navigateToEdit = () =>
    router.push({ name: "projects-management-edit", params: { id: project.value.id } });

const confirmDelete = () => {
  confirm.require({
    message: t("projects.confirm-delete", { name: project.value.name }),
    header: t("projects.delete-header"),
    icon: "pi pi-exclamation-triangle",
    accept: async () => {
      try {
        await store.deleteProject(project.value);
        router.push({ name: "projects-management" });
      } catch (error) {
        console.error("Error deleting project:", error);
      }
    },
  });
};

async function onStructureDefined() {
  // Invalidate the analytics builder dashboard so navigating to it re-fetches
  // fresh data instead of showing a stale 0 from before structure was defined.
  analyticsStore.invalidateBuilderDashboard();
  await store.fetchProjects();
  try {
    const res = await projectApi.getProjectById(route.params.id);
    if (res?.data) {
      project.value = ProjectAssembler.toEntityFromResource(res.data);
    } else {
      project.value = store.getProjectById(route.params.id);
    }
  } catch (_) {
    project.value = store.getProjectById(route.params.id);
  }
  await loadUnits();
}

const unassignedClients = computed(() => {
  return projectClients.value.filter(c => !c.unitId);
});

function openAssignModal(unit) {
  selectedUnitForAssign.value = unit;
  selectedClientId.value = null;
  showAssignModal.value = true;
}

function getClientForUnit(unit) {
  if (!unit.ownerEmail) return null;
  const emailNorm = unit.ownerEmail.trim().toLowerCase();
  return projectClients.value.find(
    c => c.unitId === unit.id || (c.email && c.email.trim().toLowerCase() === emailNorm)
  ) || null;
}

function goToClientProfile(clientId) {
  if (clientId) {
    router.push({ name: 'client-profile', params: { id: clientId } });
  } else {
    router.push({ name: 'clients' });
  }
}

function navigateToCreateClient() {
  showAssignModal.value = false;
  router.push({ name: 'clients' });
}

async function assignClientToUnit() {
  if (!selectedUnitForAssign.value || !selectedClientId.value) return;
  const client = projectClients.value.find(c => c.id === selectedClientId.value);
  if (!client || !client.email) return;

  isAssigning.value = true;
  try {
    const updated = await store.assignUnitOwner(selectedUnitForAssign.value.id, client.email.trim());
    const idx = units.value.findIndex(u => u.id === selectedUnitForAssign.value.id);
    if (idx !== -1) units.value[idx] = { ...units.value[idx], ...updated };

    await Promise.all([store.fetchProjects(), loadProjectClients()]);
    try {
      const res = await projectApi.getProjectById(route.params.id);
      if (res?.data) {
        project.value = ProjectAssembler.toEntityFromResource(res.data);
      } else {
        project.value = store.getProjectById(route.params.id);
      }
    } catch (_) {
      project.value = store.getProjectById(route.params.id);
    }
    showAssignModal.value = false;
  } catch (error) {
    console.error("Error assigning client to unit:", error);
  } finally {
    isAssigning.value = false;
  }
}

function confirmClearUnit(unit) {
  confirm.require({
    message: te('projects.structure.confirm-release-message')
      ? t('projects.structure.confirm-release-message', { number: unit.roomNumber })
      : `¿Estás seguro de liberar la Unidad ${unit.roomNumber}? Esto desvinculará al cliente asignado.`,
    header: te('projects.structure.confirm-release-header')
      ? t('projects.structure.confirm-release-header')
      : 'Liberar Unidad',
    icon: 'pi pi-exclamation-triangle',
    acceptClass: 'p-button-danger',
    accept: () => clearUnitOwner(unit)
  });
}

// Clear a unit's owner email (PATCH with null). Frees the room so the project's
// occupied-units count decreases, mirroring the increment on assignment.
async function clearUnitOwner(unit) {
  savingUnit.value = unit.id;
  try {
    const updated = await store.assignUnitOwner(unit.id, null);
    const idx = units.value.findIndex(u => u.id === unit.id);
    // Force ownerEmail to null in case the API omits null fields from the resource.
    if (idx !== -1) units.value[idx] = { ...units.value[idx], ...updated, ownerEmail: null };
    await Promise.all([store.fetchProjects(), loadProjectClients()]);
    try {
      const res = await projectApi.getProjectById(route.params.id);
      if (res?.data) {
        project.value = ProjectAssembler.toEntityFromResource(res.data);
      } else {
        project.value = store.getProjectById(route.params.id);
      }
    } catch (_) {
      project.value = store.getProjectById(route.params.id);
    }
  } catch (error) {
    console.error("Error clearing unit owner:", error);
  } finally {
    savingUnit.value = null;
  }
}

</script>

<template>
  <div class="project-details-wrapper">
  <div v-if="project" class="project-details-root p-6 bg-white">
    <div class="flex justify-content-between align-items-center mb-6">
      <pv-button
          icon="pi pi-arrow-left"
          :label="t('projects.actions.go-back') || 'Go Back'"
          text
          @click="navigateBack"
      />
      <h1 class="text-2xl font-semibold text-center flex-1">{{ project.name }}</h1>
      <div class="flex align-items-center gap-2">
        <pv-button icon="pi pi-pencil" text rounded @click="navigateToEdit" />
        <pv-button icon="pi pi-trash" text rounded severity="danger" @click="confirmDelete" />
      </div>
    </div>

    <div class="project-hero">
      <!-- Media -->
      <div class="project-hero__media">
        <img
            v-if="project.imageUrl && !imageBroken"
            :src="project.imageUrl"
            :alt="project.name"
            class="project-hero__img"
            @error="imageBroken = true"
            loading="lazy"
        />
        <div v-else class="project-hero__placeholder">
          <i class="pi pi-building"></i>
        </div>
      </div>

      <!-- Info -->
      <div class="project-hero__info">
        <div class="project-hero__statusrow">
          <span class="project-hero__status-label">{{ t("projects.fields.status") }}</span>
          <span class="project-hero__status-badge">{{ project.statusLabel }}</span>
        </div>

        <div class="project-hero__stats">
          <div class="stat">
            <span class="stat__value">{{ units.length > 0 ? units.length : project.totalUnits }}</span>
            <span class="stat__label">{{ t("projects.fields.total-units") }}</span>
          </div>
          <div class="stat">
            <span class="stat__value">{{ units.length > 0 ? occupiedCount(units) : project.occupiedUnits }}</span>
            <span class="stat__label">{{ t("projects.fields.occupied-units") }}</span>
          </div>
        </div>

        <!-- Occupancy Progress Bar in Details -->
        <div class="occupancy-bar-wrap">
          <div class="occupancy-bar-label">
            <span>{{ t("projects.fields.occupancy-rate") || "Tasa de Ocupación" }}</span>
            <span class="font-bold text-emerald-600">{{ currentOccupancyRate }}%</span>
          </div>
          <div class="occupancy-track">
            <div
              class="occupancy-fill"
              :style="{ width: `${currentOccupancyRate}%` }"
            ></div>
          </div>
        </div>

        <dl class="project-hero__meta">
          <div class="meta-row">
            <dt><i class="pi pi-calendar"></i> {{ t("projects.fields.created-date") }}</dt>
            <dd>{{ formatDate(project.createdDate) }}</dd>
          </div>
          <div class="meta-row">
            <dt><i class="pi pi-map-marker"></i> {{ t("projects.fields.location") }}</dt>
            <dd>{{ project.location || "—" }}</dd>
          </div>
          <div v-if="project.description" class="meta-row meta-row--block">
            <dt><i class="pi pi-align-left"></i> {{ t("projects.fields.description") }}</dt>
            <dd>{{ project.description }}</dd>
          </div>
        </dl>

        <!-- Define Structure — only visible when no structure exists yet AND fetch succeeded -->
        <div v-if="!hasStructure && !unitsError">
          <pv-button
              :label="t('projects.actions.define-structure') || 'Configurar Estructura'"
              icon="pi pi-sitemap"
              class="w-full custom-green-button"
              severity="success"
              @click="showStructureDialog = true"
          />
        </div>

        <!-- Units fetch error — prevents misreading a failed load as empty project -->
        <div v-if="unitsError" class="project-hero__error">
          <i class="pi pi-exclamation-circle"></i>
          {{ te('projects.structure.load-error') ? t('projects.structure.load-error') : 'Could not load project structure. Please refresh and try again.' }}
        </div>
      </div>
    </div>

    <!-- Structure display — shown once the project has units -->
    <div v-if="hasStructure" class="mt-8">
      <div class="flex align-items-center gap-2 mb-4">
        <i class="pi pi-building section-icon text-lg"></i>
        <h2 class="text-lg font-semibold text-gray-800">{{ te('projects.structure.title') ? t('projects.structure.title') : 'Estructura del Proyecto' }}</h2>
        <pv-tag :value="te('projects.structure.units-count') ? t('projects.structure.units-count', { count: units.length }) : `${units.length} unidades`" severity="success" />
        <div class="ml-auto flex align-items-center gap-2">
          <pv-button
              :label="te('projects.structure.manage-in-clients') ? t('projects.structure.manage-in-clients') : 'Gestionar en Clientes'"
              icon="pi pi-users"
              severity="secondary"
              outlined
              size="small"
              @click="router.push({ name: 'clients' })"
          />
        </div>
      </div>

      <!-- Per-floor unit grid from the units API -->
      <div v-if="structureGrid.length > 0" class="structure-floors">
        <section v-for="row in structureGrid" :key="row.floor" class="floor-card">
          <header class="floor-card__head">
            <span class="floor-card__name">{{ te('projects.structure.floor') ? t('projects.structure.floor', { number: row.floor }) : `Piso ${row.floor}` }}</span>
            <span class="floor-card__occ">
              <i class="pi pi-user"></i>
              {{ te('projects.structure.occupied-summary') ? t('projects.structure.occupied-summary', { occupied: occupiedCount(row.units), total: row.units.length }) : `${occupiedCount(row.units)}/${row.units.length} ocupadas` }}
            </span>
          </header>

          <div class="unit-grid">
            <template v-for="unit in row.units" :key="unit.id">
              <!-- Occupied unit -->
              <div
                  v-if="unit.ownerEmail"
                  class="unit-card unit-card--occupied"
                  :title="te('projects.structure.occupied-by') ? t('projects.structure.occupied-by', { name: getClientForUnit(unit)?.fullName || unit.ownerEmail }) : `Ocupada: ${getClientForUnit(unit)?.fullName || unit.ownerEmail}`"
              >
                <div class="unit-card__top">
                  <div class="flex align-items-center gap-2">
                    <i class="pi pi-user"></i>
                    <span class="unit-card__no">{{ unit.roomNumber }}</span>
                  </div>
                  <pv-tag :value="te('projects.structure.occupied') ? t('projects.structure.occupied') : 'Ocupada'" severity="success" class="text-xs" />
                </div>
                <div class="unit-card__body">
                  <div
                      class="unit-card__client-name"
                      :title="getClientForUnit(unit)?.fullName || unit.ownerEmail"
                  >
                    {{ getClientForUnit(unit)?.fullName || unit.ownerEmail }}
                  </div>
                  <div
                      v-if="getClientForUnit(unit)?.fullName"
                      class="unit-card__client-email"
                      :title="unit.ownerEmail"
                  >
                    {{ unit.ownerEmail }}
                  </div>
                </div>
                <div class="unit-card__actions">
                  <pv-button
                      v-if="getClientForUnit(unit)"
                      :label="te('projects.structure.view-client') ? t('projects.structure.view-client') : 'Ver Cliente'"
                      icon="pi pi-external-link"
                      size="small"
                      text
                      class="p-0 text-xs"
                      @click="goToClientProfile(getClientForUnit(unit).id)"
                  />
                  <span v-else></span>
                  <pv-button
                      icon="pi pi-times"
                      severity="danger"
                      text
                      rounded
                      size="small"
                      :loading="savingUnit === unit.id"
                      :title="te('projects.structure.release-unit') ? t('projects.structure.release-unit') : 'Liberar Unidad'"
                      @click="confirmClearUnit(unit)"
                  />
                </div>
              </div>

              <!-- Vacant unit -->
              <div
                  v-else
                  class="unit-card unit-card--vacant"
              >
                <div class="unit-card__top">
                  <div class="flex align-items-center gap-2">
                    <i class="pi pi-home"></i>
                    <span class="unit-card__no">{{ unit.roomNumber }}</span>
                  </div>
                  <pv-tag :value="te('projects.structure.available') ? t('projects.structure.available') : 'Disponible'" severity="info" class="text-xs" />
                </div>
                <div class="unit-card__body">
                  <span class="text-xs text-gray-400 italic">{{ te('projects.structure.no-owner') ? t('projects.structure.no-owner') : 'Sin propietario' }}</span>
                </div>
                <div class="unit-card__actions">
                  <pv-button
                      :label="te('projects.structure.assign-client') ? t('projects.structure.assign-client') : 'Asignar Cliente'"
                      icon="pi pi-user-plus"
                      size="small"
                      text
                      class="w-full text-xs p-0"
                      @click="openAssignModal(unit)"
                  />
                </div>
              </div>
            </template>
          </div>
        </section>
      </div>

      <!-- Fallback while units load -->
      <div v-else class="units-loading-hint p-3 text-sm">
        <i class="pi pi-info-circle mr-2"></i>
        <span>{{ te('projects.structure.loading-hint') ? t('projects.structure.loading-hint', { total: project.totalUnits, occupied: project.occupiedUnits }) : `This project has ${project.totalUnits} total unit(s) and ${project.occupiedUnits} occupied.` }}</span>
      </div>
    </div>
  </div>

  <div v-else class="text-center py-8">
    <div class="inline-flex align-items-center justify-content-center w-4rem h-4rem border-circle bg-gray-100 text-gray-500 mb-3">
      <i class="pi pi-building text-2xl"></i>
    </div>
    <h3 class="text-lg font-semibold text-gray-800 mb-2">{{ te('projects.structure.project-not-found') ? t('projects.structure.project-not-found') : 'Proyecto no encontrado' }}</h3>
    <p class="text-gray-500 text-sm mb-4">{{ t("projects.messages.no-projects") }}</p>
    <pv-button
      :label="t('projects.actions.go-back') || 'Regresar a Proyectos'"
      icon="pi pi-arrow-left"
      class="custom-green-button"
      @click="navigateBack"
    />
  </div>

  <!-- Assign Client Modal -->
  <pv-dialog
      v-model:visible="showAssignModal"
      modal
      :header="te('projects.structure.assign-modal-header') ? t('projects.structure.assign-modal-header', { number: selectedUnitForAssign?.roomNumber || '' }) : `Asignar Cliente - Unidad ${selectedUnitForAssign?.roomNumber || ''}`"
      :style="{ width: '450px' }"
  >
    <div class="p-fluid">
      <p class="text-sm text-gray-600 mb-3">
        {{ te('projects.structure.assign-modal-desc') ? t('projects.structure.assign-modal-desc', { number: selectedUnitForAssign?.roomNumber || '' }) : `Selecciona un cliente registrado en este proyecto para asignarlo al departamento ${selectedUnitForAssign?.roomNumber}:` }}
      </p>

      <div v-if="unassignedClients.length > 0" class="mb-3">
        <label for="assignClientSelect" class="block text-sm font-semibold mb-2">{{ te('projects.structure.assign-client-label') ? t('projects.structure.assign-client-label') : 'Cliente *' }}</label>
        <pv-select
            id="assignClientSelect"
            v-model="selectedClientId"
            :options="unassignedClients"
            optionLabel="fullName"
            optionValue="id"
            :placeholder="te('projects.structure.assign-client-placeholder') ? t('projects.structure.assign-client-placeholder') : 'Selecciona un cliente...'"
            class="w-full"
        >
          <template #option="slotProps">
            <div class="flex flex-column">
              <span class="font-semibold">{{ slotProps.option.fullName }}</span>
              <span class="text-xs text-gray-500">{{ slotProps.option.email }}</span>
            </div>
          </template>
        </pv-select>
      </div>

      <div v-else class="p-3 bg-yellow-50 border-round border-1 border-yellow-200 mb-3">
        <p class="text-sm text-yellow-800 m-0">
          <i class="pi pi-exclamation-circle mr-1"></i>
          {{ te('projects.structure.assign-all-assigned-warning') ? t('projects.structure.assign-all-assigned-warning') : 'Todos los clientes registrados en este proyecto ya tienen departamento asignado, o aún no has registrado clientes.' }}
        </p>
      </div>

      <div class="text-center mt-2">
        <pv-button
            :label="te('projects.structure.register-new-client') ? t('projects.structure.register-new-client') : '+ Ir a registrar nuevo Cliente'"
            icon="pi pi-plus"
            text
            size="small"
            @click="navigateToCreateClient"
        />
      </div>
    </div>

    <template #footer>
      <pv-button
          :label="te('projects.actions.cancel') ? t('projects.actions.cancel') : 'Cancelar'"
          icon="pi pi-times"
          text
          severity="secondary"
          @click="showAssignModal = false"
      />
      <pv-button
          :label="te('projects.structure.confirm-assignment') ? t('projects.structure.confirm-assignment') : 'Confirmar Asignación'"
          icon="pi pi-check"
          severity="success"
          :disabled="!selectedClientId || isAssigning"
          :loading="isAssigning"
          @click="assignClientToUnit"
      />
    </template>
  </pv-dialog>

  <!-- Define Structure Dialog -->
  <DefineStructureDialog
      v-model:visible="showStructureDialog"
      :project-id="route.params.id"
      @structure-defined="onStructureDefined"
  />
  </div>
</template>

<style scoped>
/* Centered container with max-width, rounded corners, subtle shadow. */
.project-details-root {
  max-width: 48rem;
  margin: 0 auto;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

/* Push the "Edit owners" button to the right inside the flex row. */
.structure-edit-btn {
  margin-left: auto;
}

/* Loading/fallback hint for units — brand green tint. */
.units-loading-hint {
  background: #ecfdf5;
  border-radius: 0.5rem;
  border: 1px solid #a7f3d0;
  color: #065f46;
}

/* Brand green used across the Project Structure section. */
.section-icon {
  color: #059669;
}

/* ── Project hero (header) ─────────────────────────────────────────── */
.project-hero {
  display: grid;
  grid-template-columns: 1.6fr 1fr;
  gap: 1.5rem;
  align-items: start;
}

@media (max-width: 768px) {
  .project-hero {
    grid-template-columns: 1fr;
  }
}

.project-hero__media {
  aspect-ratio: 16 / 10;
  border-radius: 0.85rem;
  overflow: hidden;
  background: #f3f4f6;
  box-shadow: 0 6px 18px rgba(0, 0, 0, 0.08);
}

.project-hero__img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
  transition: transform 0.3s ease;
}

.project-hero__img:hover {
  transform: scale(1.03);
}

.project-hero__placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #10b981, #047857);
}

.project-hero__placeholder .pi {
  font-size: 3.25rem;
  color: #ffffff;
  opacity: 0.92;
}

.project-hero__info {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.project-hero__statusrow {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem 1rem;
  background: #ecfdf5;
  border-left: 4px solid #10b981;
  border-radius: 0.6rem;
}

.project-hero__status-label {
  font-weight: 600;
  font-size: 0.85rem;
  color: #065f46;
}

.project-hero__status-badge {
  padding: 0.25rem 0.8rem;
  background: #059669;
  color: #ffffff;
  font-size: 0.78rem;
  font-weight: 700;
  border-radius: 999px;
}

.project-hero__stats {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.stat {
  display: flex;
  flex-direction: column;
  padding: 0.75rem 1rem;
  background: #f9fafb;
  border: 1px solid #eef0f2;
  border-radius: 0.6rem;
}

.stat__value {
  font-size: 1.6rem;
  font-weight: 700;
  line-height: 1.1;
  color: #111827;
}

.stat__label {
  margin-top: 0.15rem;
  font-size: 0.72rem;
  color: #6b7280;
}

.occupancy-bar-wrap {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.6rem 0.85rem;
  background: #f9fafb;
  border: 1px solid #eef0f2;
  border-radius: 0.6rem;
}

.occupancy-bar-label {
  display: flex;
  justify-content: space-between;
  font-size: 0.75rem;
  color: #4b5563;
}

.occupancy-track {
  width: 100%;
  height: 7px;
  background: #e5e7eb;
  border-radius: 9999px;
  overflow: hidden;
}

.occupancy-fill {
  height: 100%;
  background: linear-gradient(90deg, #10b981, #059669);
  border-radius: 9999px;
  transition: width 0.4s ease;
}

.project-hero__meta {
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
  margin: 0;
  padding: 0.85rem 1rem;
  background: #f9fafb;
  border-radius: 0.6rem;
}

.meta-row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 1rem;
}

.meta-row dt {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  margin: 0;
  font-size: 0.78rem;
  font-weight: 600;
  color: #6b7280;
}

.meta-row dt .pi {
  font-size: 0.72rem;
  color: #059669;
}

.meta-row dd {
  margin: 0;
  font-size: 0.82rem;
  color: #111827;
  text-align: right;
}

.meta-row--block {
  flex-direction: column;
  align-items: stretch;
}

.meta-row--block dd {
  margin-top: 0.25rem;
  text-align: left;
  line-height: 1.4;
}

.project-hero__error {
  padding: 0.75rem;
  background: #fef2f2;
  border: 1px solid #fecaca;
  border-radius: 0.6rem;
  color: #b91c1c;
  font-size: 0.82rem;
}

.project-hero__error .pi {
  margin-right: 0.4rem;
}

.structure-floors {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

/* One card per floor. */
.floor-card {
  background: #f9fafb;
  border: 1px solid #eef0f2;
  border-radius: 0.75rem;
  padding: 1rem 1.25rem;
}

.floor-card__head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 0.85rem;
}

.floor-card__name {
  font-weight: 700;
  font-size: 0.95rem;
  color: #1f2937;
}

.floor-card__occ {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.75rem;
  font-weight: 500;
  color: #6b7280;
}

.floor-card__occ .pi {
  font-size: 0.7rem;
  color: #059669;
}

/* Responsive grid: wraps to fill the row width, scales to any unit count. */
.unit-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 0.6rem;
}

.unit-card {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 0.75rem 0.85rem;
  border: 1px solid #e5e7eb;
  border-radius: 0.6rem;
  background: #ffffff;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
  min-height: 105px;
}

.unit-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.unit-card__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.unit-card__no {
  font-weight: 700;
  font-size: 0.95rem;
  color: #111827;
}

.unit-card__body {
  margin: 0.35rem 0;
  min-height: 28px;
}

.unit-card__client-name {
  font-weight: 600;
  font-size: 0.82rem;
  color: #1f2937;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.unit-card__client-email {
  font-size: 0.72rem;
  color: #6b7280;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.unit-card__actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-top: 0.35rem;
  border-top: 1px solid rgba(0, 0, 0, 0.06);
}

/* Vacant unit — clean neutral. */
.unit-card--vacant {
  border-color: #e5e7eb;
  background: #ffffff;
}

.unit-card--vacant .pi-home {
  color: #9ca3af;
  font-size: 0.85rem;
}

/* Occupied unit — brand green accent. */
.unit-card--occupied {
  border-color: #a7f3d0;
  background: #f0fdf4;
}

.unit-card--occupied .pi-user {
  color: #059669;
  font-size: 0.85rem;
}
</style>
