<script setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const props = defineProps({
  visible: { type: Boolean, default: false },
  targetPlan: { type: Object, default: null },
  currentPlan: { type: Object, default: null },
  isProcessing: { type: Boolean, default: false }
});

const emit = defineEmits(["close", "confirm"]);

const isUpgrade = computed(() => {
  if (!props.currentPlan || !props.targetPlan) return true;
  return Number(props.targetPlan.price) > Number(props.currentPlan.price);
});

const targetFeatures = computed(() => {
  if (!props.targetPlan) return [];
  const p = props.targetPlan;
  if (Array.isArray(p.features) && p.features.length > 0) return p.features;
  if (typeof p.featuresJson === "string" && p.featuresJson.trim()) {
    try {
      const parsed = JSON.parse(p.featuresJson);
      if (Array.isArray(parsed)) return parsed;
    } catch (_) {}
  }
  return [];
});
</script>

<template>
  <pv-dialog
    :visible="props.visible"
    @update:visible="emit('close')"
    :modal="true"
    :closable="!props.isProcessing"
    :style="{ width: '540px', maxWidth: '95vw' }"
    :header="t('subscriptions.confirm-change-title')"
  >
    <div v-if="targetPlan" class="modal-body">
      <p class="modal-subtitle">
        {{ t('subscriptions.confirm-change-subtitle') }}
      </p>

      <!-- Side-by-side Comparison -->
      <div class="compare-boxes">
        <div class="compare-box current-box">
          <div>
            <span class="box-tag">{{ t('subscriptions.current-tier') }}</span>
            <h4 class="box-title">{{ currentPlan?.name || 'Starter' }}</h4>
          </div>
          <div class="box-price">
            ${{ currentPlan?.price || 0 }}
            <span class="box-price-period">/ {{ t('subscriptions.month') }}</span>
          </div>
        </div>

        <div class="compare-box target-box">
          <span class="pill-badge">
            {{ isUpgrade ? t('subscriptions.upgrade') : t('subscriptions.downgrade') }}
          </span>
          <div>
            <span class="box-tag tag-target">{{ t('subscriptions.new-tier') }}</span>
            <h4 class="box-title">{{ targetPlan.name }}</h4>
          </div>
          <div class="box-price price-target">
            ${{ targetPlan.price }}
            <span class="box-price-period">/ {{ t('subscriptions.month') }}</span>
          </div>
        </div>
      </div>

      <!-- Unlocked Benefits -->
      <div class="benefits-box">
        <div class="benefits-header">
          <i class="pi pi-sparkles"></i>
          <span>{{ t('subscriptions.unlocked-benefits') }}:</span>
        </div>
        <ul class="benefits-list">
          <li v-for="(f, idx) in targetFeatures.slice(0, 5)" :key="idx" class="benefit-item">
            <i class="pi pi-check"></i>
            <span>{{ f }}</span>
          </li>
        </ul>
      </div>

      <!-- Stripe Reassurance -->
      <div class="stripe-badge">
        <i class="pi pi-shield"></i>
        <span>{{ t('subscriptions.secure-stripe') }}</span>
      </div>

      <!-- Actions -->
      <div class="modal-actions">
        <button
          type="button"
          class="btn-cancel"
          :disabled="props.isProcessing"
          @click="emit('close')"
        >
          Cancelar
        </button>

        <button
          type="button"
          class="btn-confirm"
          :disabled="props.isProcessing"
          @click="emit('confirm')"
        >
          <i v-if="props.isProcessing" class="pi pi-spin pi-spinner"></i>
          <span>{{ props.isProcessing ? t('subscriptions.connectingToStripe') : t('subscriptions.proceed-stripe') }}</span>
          <i v-if="!props.isProcessing" class="pi pi-arrow-right"></i>
        </button>
      </div>
    </div>
  </pv-dialog>
</template>

<style scoped>
.modal-body {
  padding: 0.5rem 0;
  font-family: inherit;
}

.modal-subtitle {
  font-size: 0.85rem;
  color: #64748b;
  margin: 0 0 1.25rem 0;
}

.compare-boxes {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  margin-bottom: 1.25rem;
}

.compare-box {
  border-radius: 0.85rem;
  padding: 1.1rem;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-height: 105px;
}

.current-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
}

.target-box {
  background: #ecfdf5;
  border: 2px solid #10b981;
  position: relative;
}

.pill-badge {
  position: absolute;
  top: -0.6rem;
  right: 0.75rem;
  background: #059669;
  color: #ffffff;
  font-size: 0.65rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  padding: 0.15rem 0.6rem;
  border-radius: 9999px;
  box-shadow: 0 2px 5px rgba(5, 150, 105, 0.3);
}

.box-tag {
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #94a3b8;
  display: block;
}

.tag-target {
  color: #059669;
}

.box-title {
  font-size: 1.15rem;
  font-weight: 800;
  color: #0f172a;
  margin: 0.2rem 0 0 0;
}

.box-price {
  font-size: 1.35rem;
  font-weight: 900;
  color: #475569;
  margin-top: 0.5rem;
}

.price-target {
  color: #059669;
}

.box-price-period {
  font-size: 0.75rem;
  font-weight: 500;
  color: #64748b;
}

/* Benefits */
.benefits-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 0.85rem;
  padding: 1rem 1.15rem;
  margin-bottom: 1.25rem;
}

.benefits-header {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.75rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #334155;
  margin-bottom: 0.75rem;
}

.benefits-header i {
  color: #10b981;
}

.benefits-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
}

.benefit-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.8rem;
  color: #334155;
}

.benefit-item i {
  color: #059669;
  font-size: 0.7rem;
  font-weight: 900;
}

/* Stripe badge */
.stripe-badge {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: #f1f5f9;
  padding: 0.65rem 0.85rem;
  border-radius: 0.6rem;
  font-size: 0.75rem;
  color: #475569;
  margin-bottom: 1.5rem;
}

.stripe-badge i {
  color: #10b981;
}

/* Actions */
.modal-actions {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 0.75rem;
  padding-top: 0.5rem;
}

.btn-cancel {
  background: transparent;
  border: none;
  color: #64748b;
  font-size: 0.85rem;
  font-weight: 600;
  padding: 0.65rem 1.15rem;
  border-radius: 0.65rem;
  cursor: pointer;
  transition: all 0.2s;
  font-family: inherit;
}

.btn-cancel:hover {
  background: #f1f5f9;
  color: #1e293b;
}

.btn-confirm {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: #10b981;
  color: #ffffff;
  border: none;
  font-size: 0.85rem;
  font-weight: 700;
  padding: 0.65rem 1.25rem;
  border-radius: 0.65rem;
  cursor: pointer;
  transition: all 0.2s;
  font-family: inherit;
  box-shadow: 0 4px 10px rgba(16, 185, 129, 0.25);
}

.btn-confirm:hover {
  background: #059669;
  transform: translateY(-1px);
}
</style>
