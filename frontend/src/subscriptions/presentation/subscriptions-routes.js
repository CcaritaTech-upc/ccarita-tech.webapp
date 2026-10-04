const subscriptionsRoutes = [
    {
        path: "",
        redirect: "/subscriptions/my-subscription"
    },
    {
        path: "my-subscription",
        name: "my-subscription",
        meta: { title: 'My Subscription', requiresRole: 'builder' },
        component: () => import("./views/my-subscription.vue"),
    }
];
export default subscriptionsRoutes;
