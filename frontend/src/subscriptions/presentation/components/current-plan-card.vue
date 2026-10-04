<script setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { translatePlanDescription } from '../plan-copy.js';
import { getPlanLimits } from '../../domain/model/plan-limits.js';

const { t, te, locale } = useI18n();

const props = defineProps({
  plan: { type: Object, required: true },
  subscription: { type: Object, required: true },
  isProcessing: { type: Boolean, default: false },
  totalDevices: { type: Number, default: 0 },
  activeProjects: { type: Number, default: 0 }
});

defineEmits(["renew", "cancel", "view-invoices", "compare-plans"]);

const isActive = computed(() => {
  return props.subscription?.status?.toLowerCase() === "active" ||
         (typeof props.subscription?.isActive === "function" && props.subscription.isActive());
});

const isCancelled = computed(() => {
  return props.subscription?.status?.toLowerCase() === "cancelled";
});

const renewalDateFormatted = computed(() => {
  const start = props.subscription?.startDate ? new Date(props.subscription.startDate) : new Date();
  const next = props.subscription?.endDate ? new Date(props.subscription.endDate) : new Date(start.getTime() + 30 * 24 * 60 * 60 * 1000);
  const dateLocale = locale.value === "es" ? "es-ES" : "en-US";
  return next.toLocaleDateString(dateLocale, { year: "numeric", month: "long", day: "numeric" });
});

const planLimits = computed(() => {
  return getPlanLimits(props.plan);
});

const rawDevicePercentage = computed(() => {
  if (planLimits.value.isUnlimited || planLimits.value.maxDevices === Infinity) return 15;
  if (!planLimits.value.maxDevices || planLimits.value.maxDevices <= 0) return 0;
  return Math.round((props.totalDevices / planLimits.value.maxDevices) * 100);
});

const progressWidth = computed(() => {
  if (planLimits.value.isUnlimited || planLimits.value.maxDevices === Infinity) return 15;
  return Math.min(rawDevicePercentage.value, 100);
});

const isLimitExceeded = computed(() => {
  return !planLimits.value.isUnlimited && props.totalDevices > planLimits.value.maxDevices;
});

const isNearLimit = computed(() => {
  return !planLimits.value.isUnlimited && !isLimitExceeded.value && rawDevicePercentage.value >= 80;
});
</script>

<template>
  <div class="current-plan-card">
    <!-- Notice if cancelled -->
    <div v-if="isCancelled" class="cancelled-alert">
      <div class="alert-left">
        <i class="pi pi-exclamation-triangle"></i>
        <span>{{ t('subscriptions.cancelled-notice') }}</span>
      </div>
      <span class="alert-date">{{ t('subscriptions.validThrough', { date: renewalDateFormatted }) }}</span>
    </div>

    <div class="hero-grid">
      <!-- 1. Plan Identity -->
      <div class="hero-col hero-col-identity">
        <div class="identity-top">
          <span class="eyebrow">{{ t('subscriptions.current-plan') }}</span>
          <span class="status-pill" :class="isActive ? 'status-active' : 'status-cancelled'">
            <span class="status-dot"></span>
            {{ isActive ? t('subscriptions.status-active') : t('subscriptions.status-cancelled') }}
          </span>
        </div>

        <h2 class="hero-plan-title">{{ plan.name }}</h2>
        <p class="hero-plan-desc">{{ translatePlanDescription(plan, t, te) }}</p>

        <div class="hero-price-row">
          <span class="hero-price-amount">${{ plan.price }}</span>
          <span class="hero-price-period">/ {{ t('subscriptions.month') }}</span>
        </div>

        <div class="hero-renewal">
          <i class="pi pi-calendar"></i>
          <span>{{ t('subscriptions.next-billing') }}: <strong>{{ renewalDateFormatted }}</strong></span>
        </div>
      </div>

      <!-- 2. Quotas and Meter -->
      <div class="hero-col hero-col-quotas">
        <div class="quotas-header">
          <i class="pi pi-chart-bar"></i>
          <span>{{ t('subscriptions.usage-title') }}</span>
        </div>

        <div class="quota-item">
          <div class="quota-meta">
            <span class="quota-label">{{ t('subscriptions.usage-devices') }}</span>
            <span class="quota-values">
              <strong>{{ props.totalDevices }}</strong> / {{ planLimits.label }}
              <span
                v-if="!planLimits.isUnlimited"
                class="quota-pct"
                :class="{ 'quota-pct-danger': isLimitExceeded }"
              >({{ rawDevicePercentage }}%)</span>
            </span>
          </div>
          <div class="progress-track" :class="{ 'track-danger': isLimitExceeded }">
            <div
              class="progress-fill"
              :class="{ 'progress-warning': isNearLimit, 'progress-danger': isLimitExceeded }"
              :style="{ width: `${progressWidth}%` }"
            ></div>
          </div>
          <div v-if="isLimitExceeded" class="quota-alert-box quota-alert-danger">
            <div class="quota-alert-msg">
              <i class="pi pi-exclamation-circle"></i>
              <span>{{ t('subscriptions.usage-exceeded', { current: props.totalDevices, max: planLimits.label }) }}</span>
            </div>
            <button type="button" class="quota-upgrade-btn" @click="$emit('compare-plans')">
              <i class="pi pi-arrow-up-right"></i>
              <span>{{ t('subscriptions.upgrade') }}</span>
            </button>
          </div>
          <p v-else-if="isNearLimit" class="quota-alert-text">
            <i class="pi pi-info-circle"></i>
            <span>{{ t('subscriptions.usage-warning') }}</span>
          </p>
        </div>

        <div class="quota-submeta">
          <div class="submeta-item">
            <span class="submeta-label">{{ t('subscriptions.usage-projects') }}</span>
            <span class="submeta-val">{{ t('subscriptions.activeProjectsCount', { count: props.activeProjects }) }}</span>
          </div>
          <div class="stripe-badge">
            <i class="pi pi-shield"></i>
            <span>{{ t('subscriptions.billed-via') }}</span>
          </div>
        </div>
      </div>

      <!-- 3. Actions -->
      <div class="hero-col hero-col-actions">
        <button
          v-if="isCancelled"
          type="button"
          class="hero-btn hero-btn-renew"
          :disabled="props.isProcessing"
          @click="$emit('renew')"
        >
          <i v-if="props.isProcessing" class="pi pi-spin pi-spinner"></i>
          <i v-else class="pi pi-sync"></i>
          <span>{{ t('subscriptions.reactivate-plan') }}</span>
        </button>

        <button
          v-if="isActive && isLimitExceeded"
          type="button"
          class="hero-btn hero-btn-upgrade-danger"
          @click="$emit('compare-plans')"
        >
          <i class="pi pi-arrow-circle-up"></i>
          <span>{{ t('subscriptions.upgrade') }}</span>
        </button>

        <button
          v-if="isActive"
          type="button"
          class="hero-btn hero-btn-cancel"
          :disabled="props.isProcessing"
          @click="$emit('cancel')"
        >
          <span>{{ t('subscriptions.cancel-plan') }}</span>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.current-plan-card {
  background: #ffffff;
  border-radius: 1.25rem;
  border: 1px solid #e2e8f0;
  box-shadow: 0 4px 16px -2px rgba(15, 23, 42, 0.05);
  padding: 1.75rem 2rem;
  margin-bottom: 2.5rem;
}

/* Alert */
.cancelled-alert {
  background: #fef3c7;
  border: 1px solid #fde68a;
  color: #92400e;
  font-size: 0.78rem;
  padding: 0.75rem 1rem;
  border-radius: 0.75rem;
  margin-bottom: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.alert-left {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.alert-date {
  font-weight: 700;
  color: #78350f;
}

/* Grid Layout */
.hero-grid {
  display: grid;
  grid-template-columns: 1fr 1.3fr 0.8fr;
  gap: 2rem;
  align-items: center;
}

.hero-col {
  display: flex;
  flex-direction: column;
}

.hero-col-identity {
  border-right: 1px solid #f1f5f9;
  padding-right: 2rem;
}

.hero-col-quotas {
  border-right: 1px solid #f1f5f9;
  padding-right: 2rem;
}

.hero-col-actions {
  gap: 0.65rem;
  justify-content: center;
}

/* 1. Identity Styles */
.identity-top {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.eyebrow {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #64748b;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.2rem 0.65rem;
  border-radius: 9999px;
  font-size: 0.7rem;
  font-weight: 700;
}

.status-dot {
  width: 0.4rem;
  height: 0.4rem;
  border-radius: 9999px;
}

.status-active {
  background: #ecfdf5;
  color: #065f46;
  border: 1px solid #a7f3d0;
}

.status-active .status-dot {
  background: #10b981;
}

.status-cancelled {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fecaca;
}

.status-cancelled .status-dot {
  background: #ef4444;
}

.hero-plan-title {
  font-size: 1.65rem;
  font-weight: 900;
  color: #0f172a;
  margin: 0 0 0.25rem 0;
  letter-spacing: -0.02em;
}

.hero-plan-desc {
  font-size: 0.8rem;
  color: #64748b;
  margin: 0 0 1rem 0;
}

.hero-price-row {
  display: flex;
  align-items: baseline;
  gap: 0.25rem;
  margin-bottom: 0.6rem;
}

.hero-price-amount {
  font-size: 2rem;
  font-weight: 900;
  color: #0f172a;
  letter-spacing: -0.03em;
}

.hero-price-period {
  font-size: 0.85rem;
  color: #64748b;
  font-weight: 500;
}

.hero-renewal {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.72rem;
  color: #64748b;
}

.hero-renewal i {
  color: #94a3b8;
}

.hero-renewal strong {
  color: #1e293b;
}

/* 2. Quotas Styles */
.quotas-header {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  font-size: 0.72rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #475569;
  margin-bottom: 1.25rem;
}

.quotas-header i {
  color: #10b981;
}

.quota-item {
  margin-bottom: 1.25rem;
}

.quota-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.78rem;
  margin-bottom: 0.45rem;
}

.quota-label {
  color: #334155;
  font-weight: 600;
}

.quota-values {
  color: #0f172a;
}

.quota-pct {
  color: #94a3b8;
  font-size: 0.72rem;
  margin-left: 0.2rem;
}

.progress-track {
  width: 100%;
  height: 0.55rem;
  background: #f1f5f9;
  border-radius: 9999px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: #10b981;
  border-radius: 9999px;
  transition: width 0.4s ease;
}

.progress-fill.progress-warning {
  background: #f59e0b;
}

.progress-fill.progress-danger {
  background: #ef4444;
}

.progress-track.track-danger {
  background: #fee2e2;
}

.quota-pct.quota-pct-danger {
  color: #dc2626;
  font-weight: 700;
}

.quota-alert-text {
  font-size: 0.72rem;
  color: #b45309;
  margin-top: 0.4rem;
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.quota-alert-box.quota-alert-danger {
  margin-top: 0.6rem;
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #991b1b;
  border-radius: 0.65rem;
  padding: 0.6rem 0.85rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  font-size: 0.76rem;
  line-height: 1.35;
}

.quota-alert-msg {
  display: flex;
  align-items: flex-start;
  gap: 0.45rem;
}

.quota-alert-msg i {
  color: #dc2626;
  margin-top: 2px;
  font-size: 0.9rem;
  flex-shrink: 0;
}

.quota-upgrade-btn {
  align-self: flex-start;
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.35rem 0.75rem;
  background: #dc2626;
  color: #ffffff;
  border: none;
  border-radius: 0.45rem;
  font-size: 0.72rem;
  font-weight: 700;
  cursor: pointer;
  transition: background 0.2s ease;
}

.quota-upgrade-btn:hover {
  background: #b91c1c;
}

.hero-btn-upgrade-danger {
  background: #dc2626;
  color: #ffffff;
  border: 1px solid #dc2626;
  box-shadow: 0 4px 10px rgba(220, 38, 38, 0.25);
}

.hero-btn-upgrade-danger:hover {
  background: #b91c1c;
  border-color: #b91c1c;
}

.quota-submeta {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.submeta-item {
  display: flex;
  justify-content: space-between;
  font-size: 0.78rem;
}

.submeta-label {
  color: #64748b;
}

.submeta-val {
  font-weight: 700;
  color: #1e293b;
}

.stripe-badge {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.7rem;
  color: #94a3b8;
}

.stripe-badge i {
  color: #10b981;
}

/* 3. Action Buttons */
.hero-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.7rem 1rem;
  border-radius: 0.75rem;
  font-size: 0.8rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s ease;
  border: none;
  font-family: inherit;
  outline: none;
}

.hero-btn-renew {
  background: #10b981;
  color: #ffffff;
  box-shadow: 0 4px 10px rgba(16, 185, 129, 0.2);
}

.hero-btn-renew:hover {
  background: #059669;
}

.hero-btn-invoices {
  background: #ffffff;
  color: #334155;
  border: 1.5px solid #cbd5e1;
}

.hero-btn-invoices:hover {
  background: #f8fafc;
  border-color: #94a3b8;
}

.hero-btn-cancel {
  background: transparent;
  color: #ef4444;
  font-size: 0.75rem;
  padding: 0.4rem;
}

.hero-btn-cancel:hover {
  color: #b91c1c;
  text-decoration: underline;
}

/* Responsive */
@media (max-width: 1024px) {
  .hero-grid {
    grid-template-columns: 1fr;
    gap: 1.5rem;
  }
  .hero-col-identity,
  .hero-col-quotas {
    border-right: none;
    border-bottom: 1px solid #f1f5f9;
    padding-right: 0;
    padding-bottom: 1.5rem;
  }
}
</style>
