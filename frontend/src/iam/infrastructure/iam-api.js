import { BaseApi } from "../../shared/infrastructure/base-api.js";

const usersEndpoint = import.meta.env.VITE_USERS_ENDPOINT_PATH;
const sessionsEndpoint = import.meta.env.VITE_IAM_SESSIONS_PATH || import.meta.env.VITE_AUTH_ENDPOINT_PATH || '/sessions';

/**
 * IAM API class to interact with authentication endpoints
 * @extends BaseApi
 * @class
 */
export class IamApi extends BaseApi {
    constructor() {
        super();
    }

    /**
     * Sign in with email and password
     * @param {Object} signInResource - Object containing email and password
     * @returns {Promise} Response with authenticated user and token
     */
    signIn(signInResource) {
        return this.http.post(sessionsEndpoint, signInResource);
    }

    /**
     * Sign up with email, password and role
     * @param {Object} signUpResource - Object containing email, password and role
     * @returns {Promise} Response with created user
     */
    signUp(signUpResource) {
        return this.http.post(`${usersEndpoint}`, signUpResource);
    }

    /**
     * Sign out by revoking the current bearer token server-side.
     * Backend contract: DELETE /api/v1/sessions/current -> 204.
     * @returns {Promise} Response with no content
     */
    signOut() {
        return this.http.delete(`${sessionsEndpoint}/current`);
    }

    /**
     * Get all users
     * @returns {Promise} Response with all users
     */
    getUsers() {
        return this.http.get(usersEndpoint);
    }

    /**
     * Get user by ID
     * @param {number} id - User ID
     * @returns {Promise} Response with user data
     */
    getUserById(id) {
        return this.http.get(`${usersEndpoint}/${id}`);
    }

    /**
     * Check if builder assigned a unit or client record for this email
     * @param {string} email - Email address to check
     * @returns {Promise} Response with invitation/assignment details
     */
    checkInvitation(email) {
        return this.http.get('/authentication/invitation', { params: { email } });
    }
}
