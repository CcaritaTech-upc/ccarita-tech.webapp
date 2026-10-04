<script setup>
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const props = defineProps({
  visible: { type: Boolean, default: false },
  invoices: { type: Array, default: () => [] },
  loading: { type: Boolean, default: false },
  error: { type: String, default: "" }
});

const emit = defineEmits(["close"]);

const openReceipt = (url, invoiceId = '') => {
  if (!url) return;
  const link = document.createElement('a');
  link.href = url;
  link.download = `invoice_${invoiceId || 'receipt'}.pdf`;
  link.target = '_blank';
  link.rel = 'noopener noreferrer';
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
};
</script>

<template>
  <pv-dialog
    :visible="props.visible"
    @update:visible="emit('close')"
    :modal="true"
    :style="{ width: '720px', maxWidth: '95vw' }"
    :header="t('subscriptions.invoices-title')"
  >
    <div v-if="props.loading" class="invoices-loading">
      <pv-progress-spinner style="width: 40px; height: 40px" />
      <span class="loading-label">{{ t('subscriptions.invoicesLoading') }}</span>
    </div>

    <div v-else class="invoices-container">
      <div v-if="props.error" class="error-banner">
        <i class="pi pi-exclamation-circle"></i>
        <span>{{ props.error }}</span>
      </div>

      <div v-if="!props.invoices.length && !props.error" class="empty-invoices">
        <div class="empty-icon-circle">
          <i class="pi pi-receipt"></i>
        </div>
        <h4 class="empty-title">{{ t('subscriptions.no-invoices-found') }}</h4>
        <p class="empty-subtitle">{{ t('subscriptions.invoicesEmptyDescription') }}</p>
      </div>

      <div v-else-if="props.invoices.length" class="table-container">
        <table class="invoices-table">
          <thead>
            <tr>
              <th>{{ t('subscriptions.invoice-date') }}</th>
              <th>{{ t('subscriptions.invoice-description') }}</th>
              <th>{{ t('subscriptions.invoice-amount') }}</th>
              <th>{{ t('subscriptions.invoice-status') }}</th>
              <th class="th-action">{{ t('subscriptions.invoice-receipt') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(inv, idx) in props.invoices" :key="idx" class="invoice-row">
              <td class="td-date">{{ inv.date }}</td>
              <td class="td-desc">{{ inv.description || t('subscriptions.invoice-description-default') }}</td>
              <td class="td-amount">
                ${{ Number(inv.amount).toFixed(2) }}
                <span class="currency-tag">{{ inv.currency || 'USD' }}</span>
              </td>
              <td class="td-status">
                <span v-if="inv.status === 'paid'" class="status-pill pill-paid">
                  <i class="pi pi-check"></i>
                  <span>{{ t('subscriptions.invoice-status-paid') }}</span>
                </span>
                <span v-else class="status-pill pill-pending">
                  <i class="pi pi-clock"></i>
                  <span>{{ t('subscriptions.invoice-status-pending') }}</span>
                </span>
              </td>
              <td class="td-action">
                <button
                  v-if="inv.downloadUrl"
                  type="button"
                  class="download-btn"
                  @click="openReceipt(inv.downloadUrl, inv.id)"
                >
                  <i class="pi pi-download"></i>
                  <span>{{ t('subscriptions.download-receipt') }}</span>
                </button>
                <span v-else class="no-download">—</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="modal-footer">
        <button type="button" class="btn-close" @click="emit('close')">
          {{ t('subscriptions.close') }}
        </button>
      </div>
    </div>
  </pv-dialog>
</template>

<style scoped>
.invoices-container {
  font-family: inherit;
  padding: 0.25rem 0;
}

.invoices-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3.5rem 0;
  gap: 0.75rem;
}

.loading-label {
  font-size: 0.82rem;
  color: #64748b;
}

.error-banner {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 0.65rem;
  font-size: 0.8rem;
  margin-bottom: 1.25rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.empty-invoices {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3rem 1.5rem;
  text-align: center;
}

.empty-icon-circle {
  width: 4rem;
  height: 4rem;
  border-radius: 9999px;
  background: #f1f5f9;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 1rem;
}

.empty-icon-circle i {
  font-size: 1.5rem;
  color: #94a3b8;
}

.empty-title {
  font-size: 1rem;
  font-weight: 700;
  color: #1e293b;
  margin: 0 0 0.35rem 0;
}

.empty-subtitle {
  font-size: 0.82rem;
  color: #64748b;
  max-width: 380px;
  margin: 0;
  line-height: 1.45;
}

/* Table */
.table-container {
  overflow-x: auto;
}

.invoices-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.82rem;
  text-align: left;
}

.invoices-table th {
  padding: 0.85rem 1rem;
  font-size: 0.72rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #64748b;
  border-bottom: 2px solid #e2e8f0;
  background: #f8fafc;
}

.th-action {
  text-align: right;
}

.invoice-row {
  border-bottom: 1px solid #f1f5f9;
  transition: background-color 0.15s;
}

.invoice-row:hover {
  background-color: #f8fafc;
}

.invoice-row td {
  padding: 0.9rem 1rem;
}

.td-date {
  color: #475569;
  font-weight: 600;
}

.td-desc {
  color: #0f172a;
  font-weight: 700;
}

.td-amount {
  font-weight: 800;
  color: #0f172a;
}

.currency-tag {
  font-size: 0.65rem;
  font-weight: 700;
  color: #94a3b8;
  text-transform: uppercase;
  margin-left: 0.2rem;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.2rem 0.6rem;
  border-radius: 9999px;
  font-size: 0.7rem;
  font-weight: 700;
}

.pill-paid {
  background: #ecfdf5;
  color: #065f46;
  border: 1px solid #a7f3d0;
}

.pill-paid i {
  font-size: 0.55rem;
}

.pill-pending {
  background: #fffbeb;
  color: #92400e;
  border: 1px solid #fde68a;
}

.pill-pending i {
  font-size: 0.55rem;
}

.td-action {
  text-align: right;
}

.download-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  background: transparent;
  color: #059669;
  border: 1px solid #a7f3d0;
  padding: 0.35rem 0.75rem;
  border-radius: 0.5rem;
  font-size: 0.75rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s;
  font-family: inherit;
}

.download-btn:hover {
  background: #ecfdf5;
  border-color: #059669;
}

.download-btn i {
  font-size: 0.7rem;
}

.no-download {
  color: #cbd5e1;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 1.25rem;
  border-top: 1px solid #f1f5f9;
  margin-top: 0.75rem;
}

.btn-close {
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  color: #475569;
  font-weight: 700;
  font-size: 0.85rem;
  padding: 0.55rem 1.25rem;
  border-radius: 0.65rem;
  cursor: pointer;
  transition: all 0.2s;
  font-family: inherit;
}

.btn-close:hover {
  background: #e2e8f0;
  color: #0f172a;
}
</style>
