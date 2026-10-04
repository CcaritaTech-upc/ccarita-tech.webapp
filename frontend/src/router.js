import {createRouter, createWebHistory} from "vue-router";
import automationRoutes from "./devices/presentation/automation-routes.js";
import subscriptionsRoutes from "./subscriptions/presentation/subscriptions-routes.js";
import profilesRoutes from "./profiles/presentation/profiles-routes.js";
import projectsRoutes from "./projects/presentation/projects-routes.js";
import analyticsRoutes from "./analytics/presentation/analytics-routes.js";
import { clientsRoutes } from "./clients/presentation/clients-routes.js";
import iamRoutes from "./iam/presentation/iam-routes.js";
import { SubscriptionApi } from "./subscriptions/infrastructure/subscription-api.js";
import { TOKEN_KEY, CURRENT_USER_KEY } from "./shared/infrastructure/storage-keys.js";
import { ROUTES } from "./shared/infrastructure/paths.js";
import { RETRY_DELAY_SHORT_MS } from "./shared/infrastructure/constants.js";
import { isActiveStatus } from "./subscriptions/domain/model/subscription-status.enum.js";

const subscriptionApi = new SubscriptionApi();

// A builder may only use the platform once they hold an active subscription.
// Returns true when the current builder has an active subscription (fail-closed
// on error so paid features stay gated).
async function builderHasActiveSubscription() {
    try {
        const currentUser = JSON.parse(localStorage.getItem(CURRENT_USER_KEY) || 'null');
        if (!currentUser?.id) return false;
        const { data } = await subscriptionApi.getSubscriptionByBuilderId(currentUser.id);
        return !!data && isActiveStatus(data.status, data.endDate);
    } catch (error) {
        console.error('[subscription-gate] check failed:', error);
        return false;
    }
}

const routes = [

    {
        path: '/iam',
        name: 'iam',
        children: iamRoutes
    },
    {
        path: '/analytics',
        children: analyticsRoutes
    },
    {
        path: '/devices',
        children: automationRoutes
    },
    {
        path: '/profiles',
        name: 'profiles',
        redirect: '/profiles/profile',
        children: profilesRoutes
    },
    {
        path: '/projects',
        meta: { requiresRole: 'builder' },
        children: projectsRoutes
    },
    {
        path: '/clients',
        name: 'clients-module',
        meta: { requiresRole: 'builder' },
        children: clientsRoutes
    },
    {
        path: '/subscriptions',
        meta: { requiresRole: 'builder' },
        children:  subscriptionsRoutes
    },
    {
        path: '/',
        redirect: ROUTES.IAM_LOGIN
    },
    {
        path: '/home',
        name: 'home',
        redirect: ROUTES.ANALYTICS_DASHBOARD
    },

    {
        path: '/:pathMatch(.*)*',
        name: 'NotFound',
        component: () => import('./shared/presentation/views/page-not-found.vue'),
    }
]

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: routes
});

router.beforeEach(async (to, from, next) => {
    // Check if route requires authentication
    const isPublicRoute = to.meta?.public === true;
    const token = localStorage.getItem(TOKEN_KEY);
    const isAuthenticated = !!token;

    // If route is not public and user is not authenticated, redirect to login page
    if (!isPublicRoute && !isAuthenticated) {
        next(ROUTES.IAM_LOGIN);
        return;
    }
    // If user is authenticated and trying to access login, redirect to home
    if (to.path === ROUTES.IAM_LOGIN && isAuthenticated) {
        next(ROUTES.HOME);
        return;
    }

    // ── Role gate: RBAC validation ──────────────────────────────────────
    // Check if route or any parent route requires a specific role.
    const requiredRoleRecord = to.matched.slice().reverse().find(record => record.meta?.requiresRole);
    if (requiredRoleRecord && isAuthenticated) {
        const currentUser = JSON.parse(localStorage.getItem(CURRENT_USER_KEY) || 'null');
        const userRole = String(currentUser?.role ?? '').toLowerCase();
        const requiredRole = String(requiredRoleRecord.meta.requiresRole).toLowerCase();
        if (userRole !== requiredRole) {
            // Unauthorized role: redirect to their own dashboard
            next(ROUTES.ANALYTICS_DASHBOARD);
            return;
        }
    }

    // ── Subscription gate (builders only) ──────────────────────────────
    // A builder without an active subscription may still access their profile,
    // dashboard and subscription management. Restricted features (projects, clients)
    // redirect to subscriptions.
    if (isAuthenticated) {
        const currentUser = JSON.parse(localStorage.getItem(CURRENT_USER_KEY) || 'null');
        const isBuilder = String(currentUser?.role).toLowerCase() === 'builder';
        const isAllowedWithoutSub = to.path.startsWith(ROUTES.SUBSCRIPTIONS_BASE)
            || to.path.startsWith(ROUTES.IAM_BASE)
            || to.path.startsWith(ROUTES.PROFILES_BASE);

        if (isBuilder && !isAllowedWithoutSub) {
            // Retry up to 2 times with a short delay to handle the race window
            // between Stripe checkout redirect and the incoming webhook setting
            // status to 'active'. Without this, the first navigation after
            // checkout fails the gate before the webhook fires.
            let active = await builderHasActiveSubscription();
            if (!active) {
                await new Promise((r) => setTimeout(r, RETRY_DELAY_SHORT_MS));
                active = await builderHasActiveSubscription();
            }
            if (!active) {
                next(ROUTES.SUBSCRIPTION_DETAIL);
                return;
            }
        }
    }

    next();
});

router.afterEach((to) => {
    // Synchronize document.title after navigation resolves
    const baseTitle = 'IoBuild';
    const title = to.meta?.title || to.matched.slice().reverse().find(r => r.meta?.title)?.meta?.title;
    document.title = title ? `${baseTitle} - ${title}` : baseTitle;
});

export default router;