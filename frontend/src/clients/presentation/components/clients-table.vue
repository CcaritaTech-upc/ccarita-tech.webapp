<script setup>
import { ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { ClientStatus } from '../../domain/model/client-status.enum.js';

const { t, locale } = useI18n();
const router = useRouter();
const currentPageReportTemplate = computed(() => locale.value.startsWith('es')
  ? '{first} a {last} de {totalRecords}'
  : '{first} to {last} of {totalRecords}');

defineProps({
  clients: {
    type: Array,
    required: true
  },
  loading: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['edit', 'delete']);

const menu = ref();
const selectedClient = ref(null);

const menuItems = computed(() => [
  {
    label: t('clients.actions.edit'),
    icon: 'pi pi-pencil',
    command: () => {
      emit('edit', selectedClient.value);
    }
  },
  {
    label: t('clients.actions.delete'),
    icon: 'pi pi-trash',
    command: () => {
      emit('delete', selectedClient.value);
    }
  }
]);

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

const getStatusLabel = (status) => {
  const statusMap = {
    [ClientStatus.ACTIVE]: 'active',
    [ClientStatus.STAND_BY]: 'standBy',
    [ClientStatus.SUSPENDED]: 'suspended'
  };
  return t(`clients.status.${statusMap[status] || 'active'}`);
};

const viewProfile = (client) => {
  router.push({ name: 'client-profile', params: { id: client.id } });
};

const toggleMenu = (event, client) => {
  selectedClient.value = client;
  menu.value.toggle(event);
};
</script>

<template>
  <div>
    <pv-menu ref="menu" :model="menuItems" :popup="true">
      <template #item="{ item }">
        <div class="p-menu-item-content" @click="item.command">
          <span :class="item.icon" class="mr-2"></span>
          <span>{{ item.label }}</span>
        </div>
      </template>
    </pv-menu>

    <pv-data-table
      :value="clients"
      :loading="loading"
      stripedRows
      paginator
      :rows="10"
      :rowsPerPageOptions="[5, 10, 20, 50]"
      tableStyle="min-width: 50rem"
      paginatorTemplate="RowsPerPageDropdown FirstPageLink PrevPageLink CurrentPageReport NextPageLink LastPageLink"
      :currentPageReportTemplate="currentPageReportTemplate"
      class="clients-table"
    >
      <template #empty>
        <div class="text-center py-4 text-gray-500">
          {{ t('clients.messages.noClients') }}
        </div>
      </template>

      <pv-column field="fullName" :header="t('clients.fields.fullName')" sortable>
        <template #body="slotProps">
          <div class="flex flex-column">
            <span class="font-semibold">{{ slotProps.data.fullName }}</span>
            <span v-if="slotProps.data.email" class="text-xs text-gray-500">{{ slotProps.data.email }}</span>
          </div>
        </template>
      </pv-column>

      <pv-column field="projectName" :header="t('clients.fields.project')" sortable>
        <template #body="slotProps">
          <span>{{ slotProps.data.projectName || t('projects.messages.no-projects') }}</span>
        </template>
      </pv-column>

      <pv-column field="unitNumber" :header="t('clients.fields.unit')" sortable>
        <template #body="slotProps">
          <pv-tag
            v-if="slotProps.data.unitNumber"
            :value="slotProps.data.unitNumber"
            severity="info"
            class="font-mono text-xs"
          />
          <span v-else class="text-xs text-gray-400 italic">{{ t('clients.fields.unassigned') }}</span>
        </template>
      </pv-column>

      <pv-column field="accountStatement" :header="t('clients.fields.accountStatement')" sortable>
        <template #body="slotProps">
          <pv-tag
            :value="getStatusLabel(slotProps.data.accountStatement)"
            :severity="getSeverity(slotProps.data.accountStatement)"
          />
        </template>
      </pv-column>

      <pv-column :header="t('clients.actions.title')" style="width: 200px">
        <template #body="slotProps">
          <div class="flex gap-2">
            <pv-button
              :label="t('clients.actions.viewProfile')"
              icon="pi pi-user"
              size="small"
              text
              @click="viewProfile(slotProps.data)"
            />
            <pv-button
              icon="pi pi-cog"
              size="small"
              text
              rounded
              @click="toggleMenu($event, slotProps.data)"
            />
          </div>
        </template>
      </pv-column>
    </pv-data-table>
  </div>
</template>

<style scoped>
:deep(.clients-table) {
  background: white !important;
}

:deep(.clients-table .p-datatable-table) {
  background: white !important;
}

:deep(.clients-table .p-datatable-thead) {
  background: #f9fafb !important;
}

:deep(.clients-table .p-datatable-thead > tr > th) {
  background: #f9fafb !important;
  color: #374151 !important;
}

:deep(.clients-table .p-datatable-tbody > tr) {
  background: white !important;
}

:deep(.clients-table .p-datatable-tbody > tr > td) {
  background: white !important;
  color: #111827 !important;
}

:deep(.clients-table .p-datatable-tbody > tr:nth-child(even)) {
  background: #f9fafb !important;
}

:deep(.clients-table .p-datatable-tbody > tr:nth-child(even) > td) {
  background: #f9fafb !important;
  color: #111827 !important;
}

:deep(.clients-table .p-datatable-tbody > tr:hover) {
  background: #f3f4f6 !important;
}

:deep(.clients-table .p-datatable-tbody > tr:hover > td) {
  background: #f3f4f6 !important;
  color: #111827 !important;
}

:deep(.clients-table .p-paginator) {
  background: white !important;
  color: #111827 !important;
}

:deep(.clients-table .p-paginator .p-paginator-pages .p-paginator-page) {
  background: white !important;
  color: #111827 !important;
}

:deep(.clients-table .p-paginator .p-paginator-pages .p-paginator-page.p-highlight) {
  background: #3b82f6 !important;
  color: white !important;
}

:deep(.clients-table .p-paginator .p-paginator-pages .p-paginator-page:hover) {
  background: #f3f4f6 !important;
  color: #111827 !important;
}

/* Forzar estilos en el span interno del dropdown */
:deep(.clients-table .p-paginator .p-dropdown span) {
  color: #111827 !important;
  background: transparent !important;
}

/* Forzar el color del p-select-label específicamente */
:deep(.clients-table .p-paginator .p-dropdown .p-select-label) {
  color: #111827 !important;
  background: white !important;
  -webkit-text-fill-color: #111827 !important;
}

:deep(.clients-table .p-paginator .p-select-label) {
  color: #111827 !important;
  background: white !important;
  -webkit-text-fill-color: #111827 !important;
}

/* Sobrescribir la variable CSS de PrimeVue */
:deep(.clients-table .p-paginator .p-dropdown) {
  --p-select-color: #111827 !important;
  background: white !important;
  border: 1px solid #d1d5db !important;
  border-radius: 0.375rem !important;
  box-shadow: none !important;
}

:deep(.clients-table .p-paginator .p-dropdown:hover) {
  border-color: #9ca3af !important;
}

:deep(.clients-table .p-paginator .p-dropdown:focus),
:deep(.clients-table .p-paginator .p-dropdown:focus-within) {
  border-color: #3b82f6 !important;
  box-shadow: 0 0 0 1px #3b82f6 !important;
  outline: none !important;
}

/* Asegurar que el select tenga un borde limpio */
:deep(.clients-table .p-paginator .p-select) {
  border: 1px solid #d1d5db !important;
  border-radius: 0.375rem !important;
  background: white !important;
  box-shadow: none !important;
}

:deep(.clients-table .p-paginator .p-select:hover) {
  border-color: #9ca3af !important;
}

:deep(.clients-table .p-paginator .p-select:focus) {
  border-color: #3b82f6 !important;
  box-shadow: 0 0 0 1px #3b82f6 !important;
  outline: none !important;
}

:deep(.clients-table .p-paginator .p-dropdown-trigger .p-icon) {
  color: #111827 !important;
}

/* Forzar el color del input select nativo si existe */
:deep(.clients-table .p-paginator select) {
  color: #111827 !important;
  background: white !important;
}

/* Estilos específicos para el valor mostrado en el dropdown */
:deep(.clients-table .p-paginator .p-dropdown .p-dropdown-label.p-inputtext) {
  color: #111827 !important;
  background: white !important;
  -webkit-text-fill-color: #111827 !important;
}

:deep(.clients-table .p-paginator .p-paginator-current) {
  color: #111827 !important;
}

/* Estilos para el panel del dropdown cuando se abre */
:deep(.p-dropdown-panel) {
  background: white !important;
}

:deep(.p-dropdown-panel .p-dropdown-items .p-dropdown-item) {
  color: #111827 !important;
  background: white !important;
}

:deep(.p-dropdown-panel .p-dropdown-items .p-dropdown-item:hover) {
  background: #f3f4f6 !important;
  color: #111827 !important;
}

:deep(.p-dropdown-panel .p-dropdown-items .p-dropdown-item.p-highlight) {
  background: #3b82f6 !important;
  color: white !important;
}

/* Forzar color en items del panel */
:deep(.p-dropdown-panel .p-dropdown-item span) {
  color: #111827 !important;
}

:deep(.p-dropdown-panel .p-dropdown-item.p-highlight span) {
  color: white !important;
}

/* Grid del formulario */
:deep(.grid) {
  background: white !important;
}

/* Asegurar que el texto en las celdas sea negro */
:deep(.clients-table .p-datatable-tbody > tr > td span) {
  color: #111827 !important;
}

:deep(.clients-table .p-datatable-tbody > tr > td .font-semibold) {
  color: #111827 !important;
}

/* Estilos para el menú popup */
:deep(.p-menu) {
  background: white !important;
}

:deep(.p-menu .p-menu-list) {
  background: white !important;
  padding: 0.25rem !important;
}

:deep(.p-menu .p-menu-item-content) {
  padding: 0.75rem 1rem !important;
  cursor: pointer !important;
  color: #111827 !important;
  background: white !important;
  border-radius: 0.375rem !important;
  transition: all 0.2s !important;
}

:deep(.p-menu .p-menu-item-content:hover) {
  background: #f3f4f6 !important;
  color: #111827 !important;
}

:deep(.p-menu .p-menu-item-content .pi) {
  color: #6b7280 !important;
}

:deep(.p-menu .p-menu-item-content:hover .pi) {
  color: #111827 !important;
}
</style>
