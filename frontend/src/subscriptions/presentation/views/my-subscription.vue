<script setup>
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";

import useSubscriptionStore from "../../application/subscription.store.js";
import { useAnalyticsStore } from "../../../analytics/application/analytics.store.js";
import { IamFacade } from "../../infrastructure/iam.facade.js";
import PlanCard from "../components/plan-card.vue";
import CurrentPlanCard from "../components/current-plan-card.vue";
import PreviousInvoicesModal from "../components/previous-invoices-modal.vue";
import ChangePlanModal from "../components/change-plan-modal.vue";
import PlanComparisonModal from "../components/plan-comparison-modal.vue";
import { SubscriptionApi } from "../../infrastructure/subscription-api.js";
import {
  TOAST_SUCCESS_DURATION_MS,
  TOAST_ERROR_DURATION_MS,
  TOAST_INVOICE_ERROR_DURATION_MS,
  TOAST_AUTH_ERROR_DURATION_MS
} from "../../../shared/infrastructure/constants.js";

// Stripe
import { getStripeClient } from "../../infrastructure/stripe-client.js";

const { t } = useI18n();
const confirm = useConfirm();
const toast = useToast();
const store = useSubscriptionStore();
const analyticsStore = useAnalyticsStore();
const subscriptionApi = new SubscriptionApi();

const isProcessing = ref(false);

// Invoices modal
const invoicesVisible = ref(false);
const invoicesLoading = ref(false);
const invoices = ref([]);
const invoicesError = ref("");

// Change plan confirmation modal
const changePlanVisible = ref(false);
const targetPlanForChange = ref(null);

// Comparison matrix modal
const comparisonVisible = ref(false);

const getBuilderId = () => {
  try {
    return IamFacade.getCurrentUserId();
  } catch (error) {
    console.error("[Subscriptions] Error getting user ID:", error);
    toast.add({
      severity: "error",
      summary: "Error de autenticación",
      detail: error.message,
      life: TOAST_AUTH_ERROR_DURATION_MS
    });
    throw error;
  }
};

const openInvoicesDialog = async () => {
  invoicesVisible.value = true;
  invoicesLoading.value = true;
  invoicesError.value = "";
  invoices.value = [];

  try {
    const builderId = getBuilderId();
    const { data } = await subscriptionApi.getInvoicesByBuilder(builderId);
    invoices.value = (data || []).map((r) => ({
      ...r,
      amount: r.amount ?? (r.amountInCents ? (r.amountInCents / 100) : 0),
      currency: r.currency || 'USD',
      downloadUrl: r.receiptUrl ?? r.downloadUrl ?? null,
      date: r.date ? new Date(r.date).toLocaleDateString("es-ES", {
        year: "numeric",
        month: "short",
        day: "numeric"
      }) : new Date().toLocaleDateString("es-ES", { year: "numeric", month: "short", day: "numeric" })
    }));
  } catch (error) {
    console.warn("Could not fetch remote Stripe invoices, falling back to local subscription data:", error);
    if (store.currentSubscription && store.currentPlan) {
      invoices.value = [{
        id: `in_sub_${store.currentSubscription.id}`,
        description: `Suscripción Plan ${store.currentPlan.name}`,
        amount: store.currentPlan.price,
        currency: 'USD',
        status: store.currentSubscription.status === 'active' ? 'paid' : store.currentSubscription.status,
        date: new Date(store.currentSubscription.startDate || Date.now()).toLocaleDateString("es-ES", {
          year: "numeric",
          month: "short",
          day: "numeric"
        }),
        downloadUrl: null
      }];
    } else {
      invoices.value = [];
    }
  } finally {
    invoicesLoading.value = false;
  }
};

onMounted(async () => {
  store.fetchAvailablePlans();
  store.fetchCurrentSubscription();

  try {
    const builderId = getBuilderId();
    if (builderId) {
      analyticsStore.fetchBuilderDashboard(builderId);
    }
  } catch (_) {}

  // Check URL parameters after returning from Stripe checkout
  const urlParams = new URLSearchParams(window.location.search);
  const sessionId = urlParams.get("session_id");
  const hasSuccess = urlParams.get("success") === "true";

  if (sessionId || hasSuccess) {
    try {
      if (sessionId) {
        const builderId = getBuilderId();
        await subscriptionApi.confirmPayment(builderId, sessionId);
      }

      toast.add({
        severity: "success",
        summary: t("subscriptions.success"),
        detail: t("subscriptions.payment-success") || "Pago procesado exitosamente.",
        life: TOAST_SUCCESS_DURATION_MS
      });

      await store.fetchCurrentSubscription();
    } catch (error) {
      console.error("Error confirming payment:", error);
      toast.add({
        severity: "warn",
        summary: t("subscriptions.warning") || "Advertencia",
        detail: "El pago fue procesado. Recarga la página si no ves tu nuevo plan actualizado.",
        life: TOAST_AUTH_ERROR_DURATION_MS
      });
    } finally {
      const cleanUrl = window.location.pathname;
      window.history.replaceState({}, document.title, cleanUrl);
    }
  }
});

const handleRenewPlan = async () => {
  if (!store.currentPlan) return;
  await handlePayPlan(store.currentPlan);
};

const handleCancelPlan = () => {
  confirm.require({
    message: t("subscriptions.confirm-cancel"),
    header: t("subscriptions.cancel-header"),
    icon: "pi pi-exclamation-triangle",
    acceptClass: "p-button-danger",
    accept: async () => {
      try {
        await store.cancelSubscription();
        toast.add({
          severity: "success",
          summary: t("subscriptions.success"),
          detail: t("subscriptions.cancelled-successfully"),
          life: TOAST_SUCCESS_DURATION_MS
        });
      } catch (error) {
        toast.add({
          severity: "error",
          summary: t("subscriptions.error"),
          detail: t("subscriptions.cancel-failed"),
          life: TOAST_ERROR_DURATION_MS
        });
      }
    }
  });
};

const handleSelectPlan = (plan) => {
  if (store.currentPlan && store.currentPlan.id !== plan.id) {
    targetPlanForChange.value = plan;
    changePlanVisible.value = true;
    return;
  }
  handlePayPlan(plan);
};

const confirmPlanChange = async () => {
  if (!targetPlanForChange.value) return;
  const plan = targetPlanForChange.value;
  changePlanVisible.value = false;
  await handlePayPlan(plan);
};

const handlePayPlan = async (plan) => {
  try {
    if (!import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY) {
      throw new Error("Falta configurar VITE_STRIPE_PUBLISHABLE_KEY en .env");
    }

    isProcessing.value = true;
    const builderId = getBuilderId();

    const { data } = await subscriptionApi.createCheckoutSession(builderId, plan.id);

    const redirectUrl = data.checkoutUrl || data.CheckoutUrl || data.url || data.Url;
    if (redirectUrl) {
      window.location.href = redirectUrl;
      return;
    }

    const sessionId = data.sessionId || data.SessionId || data.id || data.Id;
    if (sessionId) {
      const stripe = await getStripeClient();
      if (!stripe) throw new Error("Stripe no se pudo inicializar");

      const { error } = await stripe.redirectToCheckout({ sessionId });
      if (error) {
        console.error("Stripe redirect error:", error);
        toast.add({
          severity: "error",
          summary: t("subscriptions.error"),
          detail: "No se pudo redirigir al pago.",
          life: TOAST_ERROR_DURATION_MS
        });
      }
      return;
    }

    throw new Error("No se recibió URL ni sessionId del servidor");
  } catch (err) {
    console.error("Payment error:", err);
    const message = err?.response?.data?.message || err?.message || "No se pudo iniciar el pago";
    toast.add({
      severity: "error",
      summary: t("subscriptions.error"),
      detail: message,
      life: TOAST_ERROR_DURATION_MS
    });
  } finally {
    isProcessing.value = false;
  }
};
</script>

<template>
  <div class="subscription-page">
    <div class="page-container">
      <!-- Page Header -->
      <div class="page-header">
        <div class="header-left">
          <h1 class="page-title">{{ t("subscriptions.title") }}</h1>
          <p class="page-subtitle">
            Administra el plan de tu empresa, supervisa cuotas de dispositivos IoT y descarga tus comprobantes de facturación.
          </p>
        </div>

        <div class="header-actions">
          <button
            type="button"
            class="header-outline-btn"
            @click="comparisonVisible = true"
          >
            <i class="pi pi-table"></i>
            <span>{{ t('subscriptions.compare-plans') }}</span>
          </button>

          <button
            type="button"
            class="header-outline-btn"
            @click="openInvoicesDialog"
          >
            <i class="pi pi-receipt"></i>
            <span>{{ t('subscriptions.view-invoices') }}</span>
          </button>
        </div>
      </div>

      <!-- Spinner -->
      <div v-if="store.isLoading && !store.availablePlans.length" class="loading-state">
        <pv-progress-spinner style="width: 48px; height: 48px" />
        <span class="loading-text">Cargando información de suscripción...</span>
      </div>

      <div v-else>
        <!-- 1. Hero Card: Current Plan & Live Quotas -->
        <CurrentPlanCard
          v-if="store.currentPlan"
          :plan="store.currentPlan"
          :subscription="store.currentSubscription"
          :totalDevices="analyticsStore.builderDashboard?.totalDevices || 0"
          :activeProjects="analyticsStore.builderDashboard?.activeProjectsCount || 0"
          :isProcessing="isProcessing"
          @renew="handleRenewPlan"
          @cancel="handleCancelPlan"
          @view-invoices="openInvoicesDialog"
          @compare-plans="comparisonVisible = true"
        />

        <!-- Banner for users without subscription -->
        <div v-else class="empty-state-banner">
          <div class="banner-content">
            <div class="banner-pill">
              <i class="pi pi-sparkles"></i>
              <span>Comienza a operar en IoBuild</span>
            </div>
            <h2 class="banner-title">{{ t("subscriptions.no-subscription") }}</h2>
            <p class="banner-desc">
              Elige un plan de infraestructura para conectar tus dispositivos IoT, gestionar proyectos inmobiliarios y brindar acceso a los propietarios de tus unidades.
            </p>
          </div>
          <button
            type="button"
            class="banner-cta"
            @click="comparisonVisible = true"
          >
            <span>Ver Tabla Comparativa</span>
            <i class="pi pi-arrow-right"></i>
          </button>
        </div>

        <!-- 2. Plans Grid Section -->
        <div class="plans-section">
          <div class="plans-section-header">
            <h2 class="section-title">{{ t("subscriptions.all-plans") }}</h2>
            <p class="section-subtitle">
              Escala tu infraestructura según la cantidad de dispositivos y unidades de tus proyectos inmobiliarios.
            </p>
          </div>

          <div class="plans-grid">
            <PlanCard
              v-for="plan in store.availablePlans"
              :key="plan.id"
              :plan="plan"
              :currentPlan="store.currentPlan"
              :isPopular="plan.name?.toLowerCase().includes('pro')"
              :isProcessing="isProcessing"
              @select="handleSelectPlan"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Modals -->
    <PreviousInvoicesModal
      :visible="invoicesVisible"
      :invoices="invoices"
      :loading="invoicesLoading"
      :error="invoicesError"
      @close="invoicesVisible = false"
    />

    <ChangePlanModal
      :visible="changePlanVisible"
      :targetPlan="targetPlanForChange"
      :currentPlan="store.currentPlan"
      :isProcessing="isProcessing"
      @close="changePlanVisible = false"
      @confirm="confirmPlanChange"
    />

    <PlanComparisonModal
      :visible="comparisonVisible"
      :plans="store.availablePlans"
      :currentPlanId="store.currentPlan?.id"
      :isProcessing="isProcessing"
      @close="comparisonVisible = false"
      @select-plan="handleSelectPlan"
    />
  </div>
</template>

<style scoped>
.subscription-page {
  min-height: 100vh;
  background-color: #f8fafc;
  padding: 2.5rem 2rem 5rem 2rem;
  color: #0f172a;
  font-family: inherit;
}

.page-container {
  max-width: 1240px;
  margin: 0 auto;
}

/* Page Header */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.page-title {
  font-size: 2rem;
  font-weight: 800;
  color: #0f172a;
  letter-spacing: -0.025em;
  margin: 0 0 0.35rem 0;
}

.page-subtitle {
  font-size: 0.88rem;
  color: #64748b;
  margin: 0;
  line-height: 1.5;
  max-width: 600px;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.header-outline-btn {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  background: #ffffff;
  color: #334155;
  border: 1px solid #cbd5e1;
  padding: 0.55rem 1rem;
  border-radius: 0.75rem;
  font-size: 0.8rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  font-family: inherit;
}

.header-outline-btn:hover {
  background: #f1f5f9;
  border-color: #94a3b8;
}

.header-outline-btn i {
  color: #64748b;
  font-size: 0.8rem;
}

/* Loading */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 6rem 0;
  gap: 1rem;
}

.loading-text {
  font-size: 0.88rem;
  color: #64748b;
}

/* Empty State Banner */
.empty-state-banner {
  background: linear-gradient(135deg, #059669 0%, #0f766e 100%);
  color: #ffffff;
  border-radius: 1.25rem;
  padding: 2.25rem 2.5rem;
  margin-bottom: 2.5rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 2rem;
  flex-wrap: wrap;
  box-shadow: 0 10px 25px -5px rgba(5, 150, 105, 0.25);
}

.banner-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  background: rgba(255, 255, 255, 0.2);
  color: #ffffff;
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.3rem 0.85rem;
  border-radius: 9999px;
  margin-bottom: 0.75rem;
}

.banner-title {
  font-size: 1.65rem;
  font-weight: 800;
  margin: 0 0 0.5rem 0;
  letter-spacing: -0.02em;
}

.banner-desc {
  font-size: 0.88rem;
  color: #e6fffa;
  margin: 0;
  max-width: 650px;
  line-height: 1.5;
}

.banner-cta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: #ffffff;
  color: #065f46;
  border: none;
  font-size: 0.85rem;
  font-weight: 800;
  padding: 0.85rem 1.35rem;
  border-radius: 0.75rem;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.1);
  font-family: inherit;
  white-space: nowrap;
}

.banner-cta:hover {
  background: #ecfdf5;
  transform: translateY(-2px);
}

/* Plans Section */
.plans-section-header {
  margin-bottom: 1.75rem;
}

.section-title {
  font-size: 1.4rem;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 0.25rem 0;
}

.section-subtitle {
  font-size: 0.82rem;
  color: #64748b;
  margin: 0;
}

/* Plans Grid */
.plans-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1.75rem;
  align-items: stretch;
}

/* Responsive */
@media (max-width: 1024px) {
  .plans-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 768px) {
  .subscription-page {
    padding: 1.5rem 1rem 3rem 1rem;
  }
  .plans-grid {
    grid-template-columns: 1fr;
  }
  .page-header {
    flex-direction: column;
  }
  .header-actions {
    width: 100%;
  }
  .header-outline-btn {
    flex: 1;
    justify-content: center;
  }
}
</style>
