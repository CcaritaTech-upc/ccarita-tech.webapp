import { IamApi } from "../infrastructure/iam-api.js";
import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { AuthenticatedUserAssembler } from "../infrastructure/authenticated-user.assembler.js";
import { UserAssembler } from "../infrastructure/user.assembler.js";
import { ProfileApi } from "../../profiles/infrastructure/profile-api.js";
import { useProfileStore } from "../../profiles/application/profile.store.js";
import { Profile } from "../../profiles/domain/model/profile.entity.js";
import { useSubscriptionStore } from "../../subscriptions/application/subscription.store.js";
import { useAnalyticsStore } from "../../analytics/application/analytics.store.js";
import { TOKEN_KEY, CURRENT_USER_KEY } from "../../shared/infrastructure/storage-keys.js";

const profileApi = new ProfileApi();

const iamApi = new IamApi();

export const useIamStore = defineStore('iam', () => {
    // State
    const currentUser = ref(null);
    const token = ref(localStorage.getItem(TOKEN_KEY) || '');
    const users = ref([]);
    const isAuthenticated = ref(!!localStorage.getItem(TOKEN_KEY));
    const errors = ref([]);

    // Computed
    const getCurrentUser = computed(() => currentUser.value);
    const getToken = computed(() => token.value);
    const getIsAuthenticated = computed(() => isAuthenticated.value);
    const getUserRole = computed(() => currentUser.value?.role || '');

    // Actions

    function clearAllUserStores() {
        useSubscriptionStore().clearUserSession();
        useAnalyticsStore().clearUserSession();
        const profileStore = useProfileStore();
        profileStore.profile = new Profile({});
        profileStore.profileLoaded = false;
        profileStore.viewType = '';
        profileStore.errors = [];
    }

    /**
     * Sign in with email and password
     * @param {string} email - User email
     * @param {string} password - User password
     * @returns {Promise<AuthenticatedUser>} Authenticated user with token
     */
    async function signIn(email, password) {
        // Clear previous user's cached data before loading the new session.
        clearAllUserStores();
        try {
            const response = await iamApi.signIn({ email, password });
            const authenticatedUser = AuthenticatedUserAssembler.toEntityFromResponse(response.data);

            // Save to state
            currentUser.value = {
                id: authenticatedUser.id,
                email: authenticatedUser.email,
                role: authenticatedUser.role
            };
            token.value = authenticatedUser.token;
            isAuthenticated.value = true;

            // Save to localStorage
            localStorage.setItem(TOKEN_KEY, authenticatedUser.token);
            localStorage.setItem(CURRENT_USER_KEY, JSON.stringify(currentUser.value));

            // Try to fetch profile immediately to populate username/name/photoUrl
            try {
                const profileResponse = await profileApi.getProfile(authenticatedUser.id);
                const profileData = profileResponse.data || profileResponse;
                const payload = {
                    username: profileData.username || '',
                    name: profileData.name || profileData.fullName || '',
                    photoUrl: profileData.photoUrl || ''
                };
                console.log('iam.store: fetched profile after signIn', payload);
                updateUserProfile(payload);
            } catch (pfErr) {
                console.warn('iam.store: could not fetch profile immediately after sign in', pfErr?.message || pfErr);
            }

            return authenticatedUser;
        } catch (error) {
            errors.value.push(error.response?.data?.message || 'Error during sign in');
            throw error;
        }
    }

    /**
     * Sign up with email, password and role
     * @param {Object} signUpData - Object containing email, password and role
     * @returns {Promise} Response from API
     */
    async function signUp(signUpData) {
        try {
            const response = await iamApi.signUp(signUpData);
            return response.data;
        } catch (error) {
            errors.value.push(error.response?.data?.message || 'Error during sign up');
            throw error;
        }
    }

    /**
     * Sign out current user.
     * Revokes the bearer token server-side first (business rule: logout
     * revokes the token); local session is always cleared, even if the
     * revoke call fails (e.g. offline), so the user is never stuck.
     */
    async function signOut() {
        try {
            if (token.value) {
                await iamApi.signOut();
            }
        } catch (error) {
            errors.value.push(error.response?.data?.message || 'Error during sign out');
        } finally {
            currentUser.value = null;
            token.value = '';
            isAuthenticated.value = false;
            localStorage.removeItem(TOKEN_KEY);
            localStorage.removeItem(CURRENT_USER_KEY);
            clearAllUserStores();
        }
    }

    /**
     * Load user from localStorage
     */
    function loadUserFromStorage() {
        const storedUser = localStorage.getItem(CURRENT_USER_KEY);
        const storedToken = localStorage.getItem(TOKEN_KEY);

        if (storedUser && storedToken) {
            currentUser.value = JSON.parse(storedUser);
            token.value = storedToken;
            isAuthenticated.value = true;
        }
    }

    /**
     * Update current user with profile information (username, name, photoUrl)
     * @param {Object} profileData - Profile data containing username, name, photoUrl
     */
    function updateUserProfile(profileData) {
        if (currentUser.value) {
            currentUser.value = {
                ...currentUser.value,
                username: profileData.username,
                name: profileData.name,
                photoUrl: profileData.photoUrl
            };
            localStorage.setItem(CURRENT_USER_KEY, JSON.stringify(currentUser.value));
        }
    }

    /**
     * Fetch all users
     */
    async function fetchUsers() {
        try {
            const response = await iamApi.getUsers();
            users.value = UserAssembler.toEntitiesFromResponse(response.data);
            return users.value;
        } catch (error) {
            errors.value.push(error.response?.data?.message || 'Error fetching users');
            throw error;
        }
    }

    /**
     * Fetch user by ID
     * @param {number} id - User ID
     */
    async function fetchUserById(id) {
        try {
            const response = await iamApi.getUserById(id);
            return UserAssembler.toEntityFromResponse(response.data);
        } catch (error) {
            errors.value.push(error.response?.data?.message || 'Error fetching user');
            throw error;
        }
    }

    // Initialize: load user from storage on store creation
    loadUserFromStorage();

    return {
        // State
        currentUser,
        token,
        users,
        isAuthenticated,
        errors,
        // Computed
        getCurrentUser,
        getToken,
        getIsAuthenticated,
        getUserRole,
        // Actions
        signIn,
        signUp,
        signOut,
        loadUserFromStorage,
        updateUserProfile,
        fetchUsers,
        fetchUserById
    };
});
