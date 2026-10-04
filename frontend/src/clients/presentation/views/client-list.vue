<script setup>
import { ref, onMounted, computed } from 'vue';
import { useClientStore } from '../../application/client.store.js';
import { useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';
import { useI18n } from 'vue-i18n';
import ClientAddDialog from '../components/client-add-dialog.vue';
import ClientListHeader from '../components/client-list-header.vue';
import ClientsTable from '../components/clients-table.vue';
import { TOAST_ERROR_DURATION_MS } from '../../../shared/infrastructure/constants.js';

const clientStore = useClientStore();
const router = useRouter();
const toast = useToast();
const confirm = useConfirm();
const { t } = useI18n();
const loading = ref(false);
const showAddDialog = ref(false);

const clients = computed(() => clientStore.clients);

onMounted(async () => {
  loading.value = true;
  try {
    await clientStore.fetchClients();
  } catch (error) {
    console.error('Error loading clients:', error);
  } finally {
    loading.value = false;
  }
});

const addClient = () => {
  showAddDialog.value = true;
};

const handleSaveNewClient = async (newClientData) => {
  try {
    await clientStore.addClient(newClientData);
    toast.add({
      severity: 'success',
      summary: t('clients.messages.addSuccess'),
      detail: t('clients.messages.addSuccess'),
      life: TOAST_ERROR_DURATION_MS
    });
  } catch (error) {
    const detail = error?.response?.data?.error || t('clients.messages.addError');
    toast.add({
      severity: 'error',
      summary: t('clients.messages.addError'),
      detail: detail,
      life: 5000
    });
  }
};

const handleEditClient = (client) => {
  router.push({
    name: 'client-profile',
    params: { id: client.id },
    query: { edit: 'true' }
  });
};

const handleDeleteClient = (client) => {
  confirm.require({
    message: t('clients.messages.deleteConfirmMessage', { name: client.fullName }),
    header: t('clients.messages.deleteConfirmTitle'),
    icon: 'pi pi-exclamation-triangle',
    rejectLabel: t('clients.actions.cancel'),
    acceptLabel: t('clients.actions.delete'),
    rejectClass: 'p-button-secondary p-button-outlined',
    acceptClass: 'p-button-danger',
    accept: async () => {
      try {
        await clientStore.deleteClient(client.id);
        toast.add({
          severity: 'success',
          summary: t('clients.messages.deleteSuccess'),
          detail: t('clients.messages.deleteSuccess'),
          life: TOAST_ERROR_DURATION_MS
        });
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

    <ClientListHeader :on-add-client="addClient" />

    <ClientsTable
      :clients="clients"
      :loading="loading"
      @edit="handleEditClient"
      @delete="handleDeleteClient"
    />

    <ClientAddDialog
      v-model:visible="showAddDialog"
      @save="handleSaveNewClient"
    />
  </div>
</template>

<style scoped>
.p-4 {
  background: white !important;
}
</style>
