const projectDetails = () => import("./views/project-details.vue");
const projectForm = () => import("./views/project-form.vue");
const projectGrid = () => import("./views/project-grid.vue");

const projectsRoutes=[
    {
        path: '',
        name: 'projects-management',
        component: projectGrid,
        meta: { title: 'Projects', requiresRole: 'builder' }
    },
    {
        path: 'new',
        name: 'projects-management-new',
        component: projectForm,
        meta: { title: 'New Project', requiresRole: 'builder' }
    },
    {
        path: ':id',
        name: 'projects-management-details',
        component: projectDetails,
        meta: { title: 'Project Details', requiresRole: 'builder' }
    },
    {
        path: ':id/edit',
        name: 'projects-management-edit',
        component: projectForm,
        meta: { title: 'Edit Project', requiresRole: 'builder' }
    }
];

export default projectsRoutes;