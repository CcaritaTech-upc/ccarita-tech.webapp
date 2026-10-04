export const clientsRoutes = [
    {
        path: '',
        name: 'clients',
        component: () => import('./views/client-list.vue'),
        meta: { title: 'My Clients', requiresRole: 'builder' }
    },
    {
        path: ':id',
        name: 'client-profile',
        component: () => import('./views/client-profile.vue'),
        meta: { title: 'Client Profile', requiresRole: 'builder' }
    }
];

