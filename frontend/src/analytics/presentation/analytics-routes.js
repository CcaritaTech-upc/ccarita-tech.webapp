const analyticsDashboard = () => import("./views/analytics-dashboard.view.vue");
const ownerDeviceControl = () => import("./views/analytics-dashboard.view.vue");

const analyticsRoutes = [
    {
        path: '',
        redirect: '/analytics/dashboard'
    },
    {
        path: 'dashboard',
        name: 'analytics-dashboard',
        component: analyticsDashboard,
        meta: { title: 'Analytics Dashboard' }
    },
    {
        path: 'owner/devices',
        name: 'owner-device-control',
        component: ownerDeviceControl,
        meta: {
            title: 'My Devices — Control',
            requiresRole: 'owner',
        }
    }
];

export default analyticsRoutes;
