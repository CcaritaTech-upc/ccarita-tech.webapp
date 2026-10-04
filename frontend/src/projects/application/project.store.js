import { defineStore } from "pinia";
import { ref, computed } from "vue";
import { ProjectApi } from "../infrastructure/project-api.js";
import { ProjectAssembler } from "../infrastructure/project.assembler.js";
import { CURRENT_USER_KEY } from "../../shared/infrastructure/storage-keys.js";
import { useAnalyticsStore } from "../../analytics/application/analytics.store.js";

const projectApi = new ProjectApi();

// Resolve the authenticated builder's id from the session (IAM stores it in
// localStorage after login). Returns null when there's no valid session, so
// callers can fail closed instead of falling back to another builder's data.
function getCurrentBuilderId() {
    try {
        const currentUser = JSON.parse(localStorage.getItem(CURRENT_USER_KEY) || 'null');
        return currentUser?.id ?? null;
    } catch (error) {
        console.error('[projects] could not read current user:', error);
        return null;
    }
}

export const useProjectStore = defineStore("projects", () => {
    const projects = ref([]);
    const errors = ref([]);
    const projectsLoaded = ref(false);

    const projectsCount = computed(() => (projectsLoaded ? projects.value.length : 0));

    function fetchProjects() {
        const builderId = getCurrentBuilderId();
        if (!builderId) {
            projects.value = [];
            projectsLoaded.value = true;
            return Promise.resolve();
        }
        return projectApi
            .getProjectsByBuilderId(builderId)
            .then((response) => {
                projects.value = ProjectAssembler.toEntitiesFromResponse(response);
                projectsLoaded.value = true;
            })
            .catch((error) => {
                errors.value.push(error);
            });
    }

    function getProjectById(id) {
        const idInt = parseInt(id);
        return projects.value.find((p) => p.id === idInt);
    }

    function addProject(project) {
        // Stamp ownership with the authenticated builder so the project belongs
        // to its creator (not the entity's default builderId).
        const builderId = getCurrentBuilderId();
        return projectApi
            .createProject({ ...project, builderId })
            .then((response) => {
                const resource = response.data;
                const newProject = ProjectAssembler.toEntityFromResource(resource);
                if (newProject) {
                    projects.value.push(newProject);
                }
                return newProject;
            })
            .catch((error) => {
                errors.value.push(error);
                throw error;
            });
    }

    function updateProject(project) {
        return projectApi
            .updateProject(project)
            .then((response) => {
                const idInt = parseInt(project.id);
                const index = projects.value.findIndex((p) => p.id === idInt);
                if (index !== -1) {
                    if (response?.data) {
                        const updated = ProjectAssembler.toEntityFromResource(response.data);
                        projects.value[index] = updated;
                    } else {
                        // Backend returned 204 NoContent, update existing entity properties in place
                        const current = projects.value[index];
                        current.name = project.name ?? current.name;
                        current.description = project.description ?? current.description;
                        current.location = project.location ?? current.location;
                        current.imageUrl = project.imageUrl ?? current.imageUrl;
                        if (project.totalUnits !== undefined) current.totalUnits = project.totalUnits;
                    }
                }
                projectsLoaded.value = false;
                return project;
            })
            .catch((error) => {
                errors.value.push(error);
                throw error;
            });
    }

    function deleteProject(project) {
        const projectId = project.id ?? project;
        return projectApi
            .deleteProject(projectId)
            .then(async () => {
                const index = projects.value.findIndex((p) => p.id === parseInt(projectId));
                if (index !== -1) projects.value.splice(index, 1);

                try {
                    const analyticsStore = useAnalyticsStore();
                    analyticsStore.invalidateBuilderDashboard();
                    const builderId = getCurrentBuilderId();
                    if (builderId) {
                        analyticsStore.fetchBuilderDashboard(builderId, true).catch(() => {});
                    }
                } catch (e) {
                    console.warn('[projects] could not invalidate analytics dashboard:', e);
                }

                return fetchProjects();
            })
            .catch((error) => {
                errors.value.push(error);
                throw error;
            });
    }

    const structureLoading = ref(false);
    const structureError = ref(null);

    async function defineProjectStructure(projectId, payload) {
        structureLoading.value = true;
        structureError.value = null;
        try {
            const response = await projectApi.defineStructure(projectId, payload);
            return response;
        } catch (error) {
            structureError.value = error;
            throw error;
        } finally {
            structureLoading.value = false;
        }
    }

    async function assignUnitOwner(unitId, ownerEmail) {
        const response = await projectApi.patchUnitOwner(unitId, ownerEmail);
        return response.data;
    }

    return {
        projects,
        errors,
        projectsLoaded,
        projectsCount,
        structureLoading,
        structureError,
        fetchProjects,
        getProjectById,
        addProject,
        updateProject,
        deleteProject,
        defineProjectStructure,
        assignUnitOwner,
    };
});

export default useProjectStore;
