<script setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { translatePlanDescription, translatePlanFeature } from '../plan-copy.js';

const { t, te } = useI18n();

const props = defineProps({
  plan: { type: Object, required: true },
  currentPlan: { type: Object, default: null },
  isProcessing: { type: Boolean, default: false },
  isPopular: { type: Boolean, default: false }
});

const emit = defineEmits(["select"]);

const isCurrent = computed(() => {
  return props.currentPlan && Number(props.currentPlan.id) === Number(props.plan.id);
});

const isUpgrade = computed(() => {
  if (!props.currentPlan) return false;
  return Number(props.plan.price) > Number(props.currentPlan.price);
});

const planFeatures = computed(() => {
  const p = props.plan;
  if (!p) return [];
  if (Array.isArray(p.features) && p.features.length > 0) {
    return p.features.map(feature => translatePlanFeature(p.name, feature, t, te));
  }
  if (typeof p.featuresJson === "string" && p.featuresJson.trim()) {
    try {
      const parsed = JSON.parse(p.featuresJson);
      if (Array.isArray(parsed) && parsed.length > 0) {
        return parsed.map(feature => translatePlanFeature(p.name, feature, t, te));
      }
    } catch (_) {}
  }
  if (typeof p.features === "string" && p.features.trim()) {
    try {
      const parsed = JSON.parse(p.features);
      if (Array.isArray(parsed) && parsed.length > 0) {
        return parsed.map(feature => translatePlanFeature(p.name, feature, t, te));
      }
    } catch (_) {}
  }
  return [];
});

const buttonLabel = computed(() => {
  if (isCurrent.value) return t("subscriptions.current-plan-badge");
  if (!props.currentPlan) return t('subscriptions.choose-plan', { planName: props.plan.name });
  if (isUpgrade.value) return t("subscriptions.upgrade");
  return t("subscriptions.downgrade");
});
</script>

<template>
  <div
    class="plan-card"
    :class="{
      'is-popular': isPopular,
      'is-current': isCurrent,
      'is-disabled': props.isProcessing
    }"
  >
    <!-- Badges -->
    <div v-if="isPopular" class="popular-badge">
      <i class="pi pi-star-fill"></i>
      <span>{{ t('subscriptions.most-popular') }}</span>
    </div>

    <div v-else-if="isCurrent" class="current-badge">
      {{ t('subscriptions.current-plan-badge') }}
    </div>

    <!-- Top Content -->
    <div class="card-content-top">
      <div class="card-header">
        <h3 class="plan-name">{{ plan.name }}</h3>
        <p class="plan-desc">{{ translatePlanDescription(plan, t, te) }}</p>
      </div>

      <div class="pricing-block">
        <div class="price-wrapper">
          <span class="currency-symbol">$</span>
          <span class="price-amount">{{ plan.price }}</span>
          <span class="price-period">/ {{ t("subscriptions.month") }}</span>
        </div>
        <span class="billing-frequency">{{ t('subscriptions.billing-cycle') }}</span>
      </div>

      <div class="features-block">
        <span class="features-title">{{ t('subscriptions.features-included') }}</span>
        <ul class="features-list">
          <li v-for="(feature, i) in planFeatures" :key="i" class="feature-item">
            <span class="check-icon">
              <i class="pi pi-check"></i>
            </span>
            <span class="feature-text">{{ feature }}</span>
          </li>
        </ul>
      </div>
    </div>

    <!-- Bottom Action -->
    <div class="card-footer">
      <button
        type="button"
        class="action-btn"
        :class="{
          'btn-current': isCurrent,
          'btn-primary': isPopular || isUpgrade,
          'btn-secondary': !isCurrent && !isPopular && !isUpgrade
        }"
        :disabled="isCurrent || props.isProcessing"
        @click="!isCurrent && !props.isProcessing && $emit('select', plan)"
      >
        <i v-if="props.isProcessing" class="pi pi-spin pi-spinner"></i>
        <span>{{ buttonLabel }}</span>
        <i v-if="!isCurrent && !props.isProcessing" class="pi pi-arrow-right"></i>
      </button>
    </div>
  </div>
</template>

<style scoped>
.plan-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 1.25rem;
  padding: 2.25rem 1.75rem 1.75rem 1.75rem;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  position: relative;
  transition: all 0.25s ease;
  box-shadow: 0 4px 6px -1px rgba(15, 23, 42, 0.03), 0 2px 4px -2px rgba(15, 23, 42, 0.03);
}

.plan-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 16px 24px -4px rgba(15, 23, 42, 0.08);
  border-color: #cbd5e1;
}

.plan-card.is-popular {
  border: 2px solid #10b981;
  box-shadow: 0 12px 28px -4px rgba(16, 185, 129, 0.16);
}

.plan-card.is-current {
  border: 1.5px solid #94a3b8;
  background: #f8fafc;
}

.plan-card.is-disabled {
  opacity: 0.6;
  pointer-events: none;
}

/* Badges */
.popular-badge {
  position: absolute;
  top: -0.75rem;
  left: 50%;
  transform: translateX(-50%);
  background: #10b981;
  color: #ffffff;
  font-size: 0.72rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 0.35rem 0.9rem;
  border-radius: 9999px;
  display: flex;
  align-items: center;
  gap: 0.35rem;
  box-shadow: 0 4px 10px rgba(16, 185, 129, 0.3);
  white-space: nowrap;
}

.popular-badge i {
  font-size: 0.65rem;
}

.current-badge {
  position: absolute;
  top: -0.75rem;
  left: 50%;
  transform: translateX(-50%);
  background: #334155;
  color: #ffffff;
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  padding: 0.35rem 0.9rem;
  border-radius: 9999px;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.12);
  white-space: nowrap;
}

/* Card Header */
.card-header {
  margin-bottom: 1.25rem;
}

.plan-name {
  font-size: 1.35rem;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 0.4rem 0;
}

.plan-desc {
  font-size: 0.82rem;
  color: #64748b;
  margin: 0;
  line-height: 1.45;
  min-height: 2.5rem;
}

/* Pricing */
.pricing-block {
  padding-bottom: 1.25rem;
  margin-bottom: 1.5rem;
  border-bottom: 1px solid #f1f5f9;
}

.price-wrapper {
  display: flex;
  align-items: baseline;
  gap: 0.2rem;
}

.currency-symbol {
  font-size: 1.5rem;
  font-weight: 700;
  color: #0f172a;
}

.price-amount {
  font-size: 2.5rem;
  font-weight: 900;
  color: #0f172a;
  letter-spacing: -0.03em;
  line-height: 1;
}

.price-period {
  font-size: 0.85rem;
  font-weight: 500;
  color: #64748b;
  margin-left: 0.3rem;
}

.billing-frequency {
  display: block;
  font-size: 0.72rem;
  color: #94a3b8;
  margin-top: 0.35rem;
}

/* Features */
.features-block {
  margin-bottom: 2rem;
}

.features-title {
  display: block;
  font-size: 0.72rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #475569;
  margin-bottom: 0.9rem;
}

.features-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.feature-item {
  display: flex;
  align-items: flex-start;
  gap: 0.65rem;
  font-size: 0.82rem;
  color: #334155;
  line-height: 1.4;
}

.check-icon {
  width: 1.15rem;
  height: 1.15rem;
  border-radius: 9999px;
  background: #ecfdf5;
  color: #059669;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  margin-top: 0.1rem;
}

.check-icon i {
  font-size: 0.55rem;
  font-weight: 900;
}

.feature-text {
  flex: 1;
}

/* Footer & Buttons */
.card-footer {
  padding-top: 1.25rem;
  border-top: 1px solid #f8fafc;
}

.action-btn {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.8rem 1.25rem;
  border-radius: 0.75rem;
  font-size: 0.85rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s ease;
  border: none;
  outline: none;
  font-family: inherit;
}

.action-btn i {
  font-size: 0.75rem;
}

.btn-primary {
  background: #10b981;
  color: #ffffff;
  box-shadow: 0 4px 12px rgba(16, 185, 129, 0.25);
}

.btn-primary:hover {
  background: #059669;
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(16, 185, 129, 0.35);
}

.btn-secondary {
  background: #ffffff;
  color: #1e293b;
  border: 1.5px solid #cbd5e1;
}

.btn-secondary:hover {
  background: #f8fafc;
  border-color: #94a3b8;
}

.btn-current {
  background: #f1f5f9;
  color: #64748b;
  border: 1px solid #e2e8f0;
  cursor: not-allowed;
}
</style>
