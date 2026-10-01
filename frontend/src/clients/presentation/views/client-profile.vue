<script setup>
import { ref, onMounted, computed, watch } from 'vue';
import { useClientStore } from '../../application/client.store.js';
import { useRoute, useRouter } from 'vue-router';
import { useConfirm } from 'primevue/useconfirm';
import { useToast } from 'primevue/usetoast';
import { useI18n } from 'vue-i18n';
import ClientEditDialog from '../components/client-edit-dialog.vue';
import { TOAST_ERROR_DURATION_MS } from '../../../shared/infrastructure/constants.js';
import { ClientStatus } from '../../domain/model/client-status.enum.js';
import { DeviceApi } from '../../../devices/infrastructure/device-api.js';

const clientStore = useClientStore();
const route = useRoute();
const router = useRouter();
const confirm = useConfirm();
const toast = useToast();
const { t } = useI18n();
const deviceApi = new DeviceApi();

const clientId = computed(() => parseInt(route.params.id));
const client = computed(() => clientStore.getClientById(clientId.value));
const showEditDialog = ref(false);
const unitDevices = ref([]);
const loadingDevices = ref(false);

const loadDevices = async () => {
  if (!client.value || !client.value.unitId) {
    unitDevices.value = [];
    return;
  }
  loadingDevices.value = true;
  try {
    unitDevices.value = await deviceApi.getDevicesByUnitId(client.value.unitId);
  } catch (error) {
    console.error('Error fetching unit devices:', error);
    unitDevices.value = [];
  } finally {
    loadingDevices.value = false;
  }
};

watch(() => client.value?.unitId, () => {
  loadDevices();
});

onMounted(async () => {
  if (!clientStore.clientsLoaded) {
    await clientStore.fetchClients();
  }

  await loadDevices();

  // Si viene con query parameter edit=true, abrir el diálogo automáticamente
  if (route.query.edit === 'true') {
    showEditDialog.value = true;
  }
});

const goBack = () => {
  router.push({ name: 'clients' });
};

const getSeverity = (status) => {
  switch (status) {
    case ClientStatus.ACTIVE:
      return 'success';
    case ClientStatus.STAND_BY:
      return 'warn';
    case ClientStatus.SUSPENDED:
      return 'danger';
    default:
      return 'info';
  }
};

const getDeviceIcon = (type) => {
  const val = (type || '').toLowerCase();
  if (val.includes('light')) return 'pi pi-sun';
  if (val.includes('condition') || val.includes('air')) return 'pi pi-cloud';
  if (val.includes('water')) return 'pi pi-filter';
  if (val.includes('smoke')) return 'pi pi-bell';
  if (val.includes('meter')) return 'pi pi-gauge';
  return 'pi pi-server';
};

// Función para traducir el estado
const getStatusLabel = (status) => {
  const statusMap = {
    [ClientStatus.ACTIVE]: 'active',
    [ClientStatus.STAND_BY]: 'standBy',
    [ClientStatus.SUSPENDED]: 'suspended'
  };
  return t(`clients.status.${statusMap[status] || 'active'}`);
};

const handleEdit = () => {
  showEditDialog.value = true;
};

const handleSaveEdit = async (updatedClient) => {
  try {
    await clientStore.updateClient(updatedClient);
    await loadDevices();
    toast.add({
      severity: 'success',
      summary: t('clients.messages.updateSuccess'),
      detail: t('clients.messages.updateSuccess'),
      life: TOAST_ERROR_DURATION_MS
    });
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: t('clients.messages.updateError'),
      detail: t('clients.messages.updateError'),
      life: TOAST_ERROR_DURATION_MS
    });
  }
};

const handleDelete = () => {
  confirm.require({
    message: t('clients.messages.deleteConfirmMessage', { name: client.value.fullName }),
    header: t('clients.messages.deleteConfirmTitle'),
    icon: 'pi pi-exclamation-triangle',
    rejectLabel: t('clients.actions.cancel'),
    acceptLabel: t('clients.actions.delete'),
    rejectClass: 'p-button-secondary p-button-outlined',
    acceptClass: 'p-button-danger',
    accept: async () => {
      try {
        await clientStore.deleteClient(client.value.id);
        toast.add({
          severity: 'success',
          summary: t('clients.messages.deleteSuccess'),
          detail: t('clients.messages.deleteSuccess'),
          life: TOAST_ERROR_DURATION_MS
        });
        // Redirigir a la lista después de eliminar
        setTimeout(() => {
          router.push({ name: 'clients' });
        }, 500);
      } catch (error) {
        toast.add({
          severity: 'error',
          summary: t('clients.messages.deleteError'),
          detail: t('clients.messages.deleteError'),
          life: TOAST_ERROR_DURATION_MS
        });
      }
    }
  });
};
</script>

<template>
  <div class="p-4">
    <pv-toast />

    <pv-button
      :label="t('clients.actions.goBack')"
      icon="pi pi-arrow-left"
      @click="goBack"
      text
      class="mb-4"
    />

    <pv-card v-if="client" class="client-profile-card">
      <template #title>
        <div class="flex align-items-center gap-3">
          <i class="pi pi-user text-4xl"></i>
          <div>
            <h1 class="text-3xl font-bold m-0">{{ client.fullName }}</h1>
            <pv-tag
              :value="getStatusLabel(client.accountStatement)"
              :severity="getSeverity(client.accountStatement)"
              class="mt-2"
            />
          </div>
        </div>
      </template>

      <template #content>
        <div class="grid">
          <div class="col-12 md:col-6">
            <div class="mb-4">
              <label class="block font-semibold text-gray-700 mb-2">
                <i class="pi pi-envelope mr-2"></i>{{ t('clients.fields.email') }}
              </label>
              <p class="text-lg text-gray-900">{{ client.email }}</p>
            </div>
          </div>

          <div class="col-12 md:col-6">
            <div class="mb-4">
              <label class="block font-semibold text-gray-700 mb-2">
                <i class="pi pi-phone mr-2"></i>{{ t('clients.fields.phoneNumber') }}
              </label>
              <p class="text-lg text-gray-900">{{ client.phoneNumber || t('clients.fields.notAvailable') }}</p>
            </div>
          </div>

          <div class="col-12 md:col-6">
            <div class="mb-4">
              <label class="block font-semibold text-gray-700 mb-2">
                <i class="pi pi-map-marker mr-2"></i>{{ t('clients.fields.address') }}
              </label>
              <p class="text-lg text-gray-900">{{ client.address || t('clients.fields.notAvailable') }}</p>
            </div>
          </div>

          <div class="col-12 md:col-6">
            <div class="mb-4">
              <label class="block font-semibold text-gray-700 mb-2">
                <i class="pi pi-building mr-2"></i>{{ t('clients.fields.project') }}
              </label>
              <p class="text-lg text-gray-900">{{ client.projectName || t('clients.messages.noProject') }}</p>
            </div>
          </div>

          <div class="col-12 md:col-6">
            <div class="mb-4">
              <label class="block font-semibold text-gray-700 mb-2">
                <i class="pi pi-home mr-2"></i>{{ t('clients.fields.unit') }}
              </label>
              <p class="text-lg text-gray-900">
                <pv-tag v-if="client.unitNumber" :value="t('projects.structure.unit', { number: client.unitNumber })" severity="info" />
                <span v-else class="text-gray-400 italic">{{ t('clients.fields.unassigned') }}</span>
              </p>
            </div>
          </div>

          <div class="col-12 md:col-6">
            <div class="mb-4">
              <label class="block font-semibold text-gray-700 mb-2">
                <i class="pi pi-server mr-2"></i>{{ t('clients.fields.iotDevices') }}
              </label>
              <div v-if="!client.unitId" class="text-gray-400 italic text-sm">
                {{ t('clients.messages.assignUnitForDevices') }}
              </div>
              <div v-else-if="loadingDevices" class="flex align-items-center gap-2">
                <i class="pi pi-spin pi-spinner text-blue-500"></i>
                <span class="text-gray-500 text-sm">{{ t('clients.messages.loadingDevices') }}</span>
              </div>
              <div v-else class="flex align-items-center gap-2">
                <pv-tag
                  :value="t('clients.fields.devicesCount', { count: unitDevices.length || client.deviceCount || 0 })"
                  :severity="(unitDevices.length || client.deviceCount) > 0 ? 'success' : 'warn'"
                />
              </div>
            </div>
          </div>
        </div>

        <!-- Panel detallado de Dispositivos IoT vinculados a la Unidad -->
        <div class="mt-2 mb-4 p-4 border-round bg-gray-50 border-1 border-gray-200">
          <div class="flex align-items-center justify-content-between mb-3 flex-wrap gap-2">
            <div class="flex align-items-center gap-2">
              <i class="pi pi-bolt text-yellow-600 text-xl"></i>
              <h3 class="text-xl font-bold m-0 text-gray-800">
                {{ t('clients.profile.devicesSection') }}
              </h3>
            </div>
            <pv-tag
              v-if="client.unitId"
              :value="t('clients.fields.devicesCount', { count: unitDevices.length || client.deviceCount || 0 })"
              :severity="(unitDevices.length || client.deviceCount) > 0 ? 'success' : 'secondary'"
            />
          </div>

          <div v-if="!client.unitId" class="text-center py-4 text-gray-500">
            <i class="pi pi-info-circle text-4xl text-blue-400 mb-2"></i>
            <p class="m-0 font-medium">{{ t('clients.messages.assignUnitForDevices') }}</p>
          </div>

          <div v-else-if="loadingDevices" class="text-center py-4">
            <i class="pi pi-spin pi-spinner text-3xl text-blue-500 mb-2"></i>
            <p class="text-gray-500 m-0">{{ t('clients.messages.queryingDevices') }}</p>
          </div>

          <div v-else-if="unitDevices.length === 0" class="text-center py-4 text-gray-500">
            <i class="pi pi-inbox text-4xl text-gray-400 mb-2"></i>
            <p class="m-0 font-medium">{{ t('clients.messages.noDevicesInUnit') }}</p>
          </div>

          <div v-else class="grid">
            <div
              v-for="device in unitDevices"
              :key="device.id"
              class="col-12 sm:col-6 lg:col-4"
            >
              <div class="p-3 border-round bg-white border-1 border-gray-200 shadow-1 flex align-items-center justify-content-between">
                <div class="flex align-items-center gap-3">
                  <div class="p-2 border-round bg-blue-50 text-blue-600">
                    <i :class="getDeviceIcon(device.type)" class="text-xl"></i>
                  </div>
                  <div>
                    <span class="font-bold text-gray-900 block text-base">{{ device.name }}</span>
                    <span class="text-xs text-gray-500 block">{{ device.type }} • {{ device.location || t('projects.structure.unit', { number: client.unitNumber || '' }) }}</span>
                  </div>
                </div>
                <pv-tag
                  :value="device.status || t('devices.status.online')"
                  :severity="device.status?.toLowerCase() === 'offline' ? 'danger' : 'success'"
                />
              </div>
            </div>
          </div>
        </div>

        <pv-divider />

        <div class="flex gap-2 justify-content-end">
          <pv-button
            :label="t('clients.actions.editClient')"
            icon="pi pi-pencil"
            severity="secondary"
            outlined
            @click="handleEdit"
          />
          <pv-button
            :label="t('clients.actions.deleteClient')"
            icon="pi pi-trash"
            severity="danger"
            outlined
            @click="handleDelete"
          />
        </div>
      </template>
    </pv-card>

    <pv-card v-else class="client-profile-card">
      <template #content>
        <div class="text-center py-4">
          <i class="pi pi-exclamation-triangle text-4xl text-orange-500 mb-3"></i>
          <p class="text-xl text-gray-500">{{ t('clients.messages.clientNotFound') }}</p>
          <pv-button
            :label="t('clients.actions.goBack')"
            icon="pi pi-arrow-left"
            @click="goBack"
            class="mt-3"
          />
        </div>
      </template>
    </pv-card>

    <ClientEditDialog
      v-model:visible="showEditDialog"
      :client="client"
      @save="handleSaveEdit"
    />
  </div>
</template>

<style scoped>
.p-4 {
  background: white !important;
}


:deep(.client-profile-card) {
  background: white !important;
}

:deep(.client-profile-card ) {
  background: white !important;
}

:deep(.client-profile-card) {
  background: white !important;
  color: #111827 !important;
}

:deep(.client-profile-card) {
  background: white !important;
  color: #111827 !important;
}

:deep(.client-profile-card) {
  background: white !important;
  color: #111827 !important;
}

:deep(.client-profile-card) {
  background: white !important;
  color: #111827 !important;
}


:deep(.client-profile-card h1),
:deep(.client-profile-card h2),
:deep(.client-profile-card h3),
:deep(.client-profile-card p),
:deep(.client-profile-card label),
:deep(.client-profile-card span) {
  color: #111827 !important;
}

:deep(.client-profile-card .grid) {
  background: white !important;
}


:deep(.client-profile-card) {
  border-top-color: #e5e7eb !important;
}

:deep(.client-profile-card .pi-user) {
  color: #3b82f6 !important;
}

:deep(.client-profile-card .text-3xl) {
  color: #111827 !important;
}

:deep(.client-profile-card .text-lg) {
  color: #111827 !important;
}


:deep(.client-profile-card .pi-exclamation-triangle) {
  color: #f97316 !important;
}

/* Estilos para los botones de acción */
:deep(.client-profile-card .p-button.p-button-secondary.p-button-outlined) {
  background: #d1fae5 !important;
  border-color: #10b981 !important;
  color: #047857 !important;
}

:deep(.client-profile-card .p-button.p-button-secondary.p-button-outlined:hover) {
  background: #a7f3d0 !important;
  border-color: #059669 !important;
  color: #065f46 !important;
}

:deep(.client-profile-card .p-button.p-button-danger.p-button-outlined) {
  background: #fee2e2 !important;
  border-color: #ef4444 !important;
  color: #dc2626 !important;
}

:deep(.client-profile-card .p-button.p-button-danger.p-button-outlined:hover) {
  background: #fecaca !important;
  border-color: #dc2626 !important;
  color: #991b1b !important;
}

/* Estilos para el diálogo de confirmación */
:deep(.p-confirm-dialog) {
  background: white !important;
}

:deep(.p-confirm-dialog .p-dialog-content) {
  background: white !important;
  color: #111827 !important;
}

:deep(.p-confirm-dialog .p-dialog-header) {
  background: white !important;
  color: #111827 !important;
}

:deep(.p-confirm-dialog .p-dialog-footer) {
  background: white !important;
}

:deep(.p-confirm-dialog .p-confirm-dialog-message) {
  color: #111827 !important;
}

/* Estilos para toast notifications */
:deep(.p-toast) {
  color: #111827 !important;
}

:deep(.p-toast .p-toast-message) {
  background: white !important;
  color: #111827 !important;
}
</style>
