<script setup lang="js">
import { useI18n } from "vue-i18n";
import { ref, computed, onMounted, watch } from "vue";
import { useRouter } from "vue-router";
import LanguageSwitcher from "./language-switcher.vue";
import { useProfileStore } from "../../../profiles/application/profile.store.js";
import { useIamStore } from "../../../iam/application/iam.store.js";
import { useSubscriptionStore } from "../../../subscriptions/application/subscription.store.js";
import { ROUTES } from "../../infrastructure/paths.js";
import { isActiveStatus } from "../../../subscriptions/domain/model/subscription-status.enum.js";

const { t } = useI18n();
const router = useRouter();
const drawer = ref(false);
const profileStore = useProfileStore();
const iamStore = useIamStore();
const subscriptionStore = useSubscriptionStore();

const toggleDrawer = () => {
  drawer.value = !drawer.value;
};

const currentUser = computed(() => iamStore.currentUser);
const userRole = computed(() => currentUser.value?.role?.toLowerCase() || 'builder');
const hasActiveSubscription = computed(() => isActiveStatus(subscriptionStore.currentSubscription?.status));

onMounted(async () => {
  const userId = currentUser.value?.id;
  if (userId) {
    if (!profileStore.profileLoaded || !profileStore.profile?.name) {
      await profileStore.fetchProfile(userId);
    }

    if (profileStore.profile?.name || profileStore.profile?.username) {
      const payload = {
        username: profileStore.profile.username,
        name: profileStore.profile.name,
        photoUrl: profileStore.profile.photoUrl
      };
      iamStore.updateUserProfile(payload);
    }
  }

  // Builders need their subscription state so the sidebar can gate
  // subscription-only sections (Projects, Clients).
  if (userRole.value === 'builder' && !subscriptionStore.currentSubscription) {
    try { await subscriptionStore.fetchCurrentSubscription(); } catch (_) { /* no active subscription */ }
  }
});

// Keep IAM user profile synced whenever profile store changes
watch(
  () => [profileStore.profile?.name, profileStore.profile?.photoUrl, profileStore.profile?.username],
  ([name, photoUrl, username]) => {
    if (name || username || photoUrl) {
      iamStore.updateUserProfile({ username, name, photoUrl });
    }
  }
);

const userName = computed(() => {
  return profileStore.profile?.name ||
         currentUser.value?.name ||
         profileStore.profile?.username ||
         currentUser.value?.username ||
         'Usuario';
});

const userPhoto = computed(() => {
  const photo = profileStore.profile?.photoUrl || currentUser.value?.photoUrl;
  if (photo && photo !== 'undefined' && typeof photo === 'string' && photo.trim() !== '') {
    return photo;
  }
  const name = userName.value && userName.value !== 'Usuario' ? userName.value.trim() : 'U';
  const initial = name.charAt(0).toUpperCase();
  return `data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%2240%22 height=%2240%22%3E%3Crect width=%2240%22 height=%2240%22 rx=%2220%22 fill=%22%2310B981%22/%3E%3Ctext x=%2220%22 y=%2226%22 text-anchor=%22middle%22 fill=%22white%22 font-size=%2218%22 font-weight=%22bold%22 font-family=%22sans-serif%22%3E${initial}%3C/text%3E%3C/svg%3E`;
});


const handleLogout = async () => {
  await iamStore.signOut();
  router.push(ROUTES.LOGIN);
};

const items = [

  { label: 'option.home', to: ROUTES.ANALYTICS_DASHBOARD, use_role: 'builder', type: 'builder', icon: 'pi pi-home' },
  { label: 'option.profile', to: ROUTES.PROFILES, use_role: 'builder', type: 'builder', icon: 'pi pi-user' },
  { label: 'option.projects', to: ROUTES.PROJECTS, use_role: 'builder', type: 'builder', icon: 'pi pi-folder', requiresSubscription: true },
  { label: 'option.clients', to: ROUTES.CLIENTS, use_role: 'builder', type: 'builder', icon: 'pi pi-users', requiresSubscription: true },
  { label: 'option.subscription', to: ROUTES.SUBSCRIPTION_DETAIL, use_role: 'builder', type: 'builder', icon: 'pi pi-credit-card' },


  { label: 'option.home', to: ROUTES.ANALYTICS_DASHBOARD, use_role: 'owner', type: 'owner', icon: 'pi pi-home' },
  { label: 'option.profile', to: ROUTES.PROFILES, use_role: 'owner', type: 'owner', icon: 'pi pi-user' },
  { label: 'option.device-management', to: ROUTES.DEVICES, use_role: 'owner', type: 'owner', icon: 'pi pi-microchip' },
];

const filteredItems = computed(() => {
  const currentRole = userRole.value;
  return items.filter(item => {
    if (item.requiresSubscription && !hasActiveSubscription.value) return false;
    const itemRole = String(item.use_role || 'builder').trim().toLowerCase();
    return itemRole === currentRole;
  });
});
</script>

<template>
  <div class="app-container">
    <pv-toast/>
    <pv-confirm-dialog/>

    <header class="app-header">
      <pv-toolbar class="custom-toolbar">
        <template #start>
          <div class="header-start">
            <pv-button
                class="menu-button"
                icon="pi pi-bars"
                text
                rounded
                @click="toggleDrawer"
            />            <router-link :to="ROUTES.ANALYTICS_DASHBOARD" class="logo-link">
              <img
                src="/IoBuild-Logo.png"
                alt="IoBuild Logo"
                class="header-logo"
              />
            </router-link>
          </div>
        </template>

        <template #center>
          <div class="welcome-message">
            <span class="welcome-text">{{ t('header.welcome') }}, {{ userName }}</span>
          </div>
        </template>

        <template #end>
          <div class="header-end">

            <pv-button
              class="notification-button"
              icon="pi pi-bell"
              text
              rounded
              severity="secondary"
            />


            <router-link
              :to="ROUTES.PROFILES"
              class="user-info-link"
              @click="drawer = false"
            >
              <div class="user-info">
                <img
                  :src="userPhoto"
                  :alt="userName"
                  class="user-avatar"
                />
                <div class="user-details">
                  <span class="user-name">{{ userName }}</span>
                  <span class="user-role">{{ t(`profile.${userRole}Role`) }}</span>
                </div>
              </div>
            </router-link>


            <language-switcher/>


            <pv-button
              class="logout-button"
              icon="pi pi-sign-out"
              :label="t('iam.logout')"
              text
              rounded
              severity="danger"
              @click="handleLogout"
            />
          </div>
        </template>
      </pv-toolbar>
    </header>


    <div class="body-container">

      <div v-if="drawer" class="custom-sidebar" @click.self="drawer = false">
        <div class="sidebar-content">
          <nav class="sidebar-menu">
            <router-link
                v-for="item in filteredItems"
                :key="item.label"
                :to="item.to"
                class="menu-item"
                @click="drawer = false"
            >
              <i :class="item.icon" class="menu-icon"></i>
              <span class="menu-label">{{ t(item.label) }}</span>
              <i class="pi pi-chevron-right menu-arrow"></i>
            </router-link>
            
            <!-- Botón de Logout en el sidebar -->
            <div class="menu-item logout-item" @click="handleLogout">
              <i class="pi pi-sign-out menu-icon"></i>
              <span class="menu-label">{{ t('iam.logout') }}</span>
              <i class="pi pi-chevron-right menu-arrow"></i>
            </div>
          </nav>
        </div>
      </div>

      <main class="main-content" :class="{ 'with-sidebar': drawer }">
        <router-view :key="$route.path" />
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-container {
  min-height: 100vh;
  width: 100%;
  margin: 0;
  padding: 0;
  background: linear-gradient(135deg, #f0fdf4 0%, #e6fffa 100%);
}

.app-header {
  position: sticky;
  top: 0;
  z-index: 1001;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.custom-toolbar {
  background: white !important;
  border: none;
  padding: 0.75rem 1.5rem;
  border-bottom: 1px solid #e5e7eb;
}

.header-start {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.menu-button {
  color: #374151 !important;
  transition: transform 0.2s ease;
}

.menu-button:hover {
  transform: scale(1.1);
  background: rgba(0, 0, 0, 0.05) !important;
}

.header-logo {
  height: 40px;
  width: auto;
  object-fit: contain;
}

.logo-link {
  display: flex;
  align-items: center;
  text-decoration: none;
  border-radius: 8px;
  transition: all 0.2s ease;
  padding: 0.25rem;
}

.logo-link:hover {
  background: rgba(16, 185, 129, 0.1);
  transform: scale(1.05);
}

.logo-link:active {
  transform: scale(0.98);
}

.logo-link .header-logo {
  transition: all 0.2s ease;
}

.logo-link:hover .header-logo {
  filter: brightness(1.1);
}

.welcome-message {
  display: flex;
  align-items: center;
}

.welcome-text {
  font-size: 1.125rem;
  font-weight: 600;
  color: #374151;
}

.header-end {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.notification-button {
  color: #6b7280 !important;
  position: relative;
}

.notification-button:hover {
  background: rgba(0, 0, 0, 0.05) !important;
}

.logout-button {
  color: #ef4444 !important;
  font-weight: 500;
}

.logout-button:hover {
  background: rgba(239, 68, 68, 0.1) !important;
  color: #dc2626 !important;
}

.user-info-link {
  text-decoration: none;
  color: inherit;
  border-radius: 8px;
  transition: all 0.2s ease;
}

.user-info-link:hover {
  background: rgba(16, 185, 129, 0.05);
  transform: translateY(-1px);
}

.user-info-link:active {
  transform: translateY(0);
}

.user-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem;
  border-radius: 8px;
  transition: background-color 0.2s ease;
  cursor: pointer;
}

.user-info:hover {
  background: transparent;
}

.user-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  object-fit: cover;
  border: 2px solid #e5e7eb;
}

.user-details {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
}

.user-name {
  font-size: 0.875rem;
  font-weight: 600;
  color: #374151;
  line-height: 1.2;
}

.user-role {
  font-size: 0.75rem;
  color: #6b7280;
  line-height: 1.2;
}

:deep(.custom-drawer .p-drawer) {
  width: 320px !important;
  box-shadow: 4px 0 20px rgba(0, 0, 0, 0.06);
  background: white;
  border-radius: 0;
  z-index: 1000;
}

:deep(.custom-drawer .p-drawer .p-drawer-content) {
  padding: 0 !important;
  background: white;
}


:deep(.custom-drawer .p-drawer-mask) {
  z-index: 999;
}

.drawer-header {
  padding: 1.5rem 1.5rem;
  background: linear-gradient(135deg, #34d399 0%, #2dd4bf 100%);
  color: white;
  width: 100%;
  box-sizing: border-box;
}

.drawer-brand {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.drawer-logo {
  height: 45px;
  width: auto;
  object-fit: contain;
}

.brand-info {
  display: flex;
  flex-direction: column;
}

.drawer-title {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.12);
}

.drawer-subtitle {
  margin: 0.25rem 0 0 0;
  font-size: 0.875rem;
  opacity: 0.9;
}

.drawer-content {
  padding: 1rem 0;
}

.drawer-menu {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.menu-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem 1.5rem;
  color: black;
  text-decoration: none;
  transition: all 0.3s ease;
  position: relative;
  overflow: hidden;
  margin: 0.25rem 1rem;
  border-radius: 8px;
}

.menu-item::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  height: 100%;
  width: 4px;
  background: black;
  transform: scaleY(0);
  transition: transform 0.3s ease;
  border-radius: 0 4px 4px 0;
}

.menu-item:hover {
  background: rgba(0, 0, 0, 0.1);
  color: black;
}

.menu-item:hover::before {
  transform: scaleY(1);
}

.menu-item.router-link-active {
  background: black;
  color: white;
  font-weight: 600;
  border-radius: 8px;
}

.menu-item.router-link-active::before {
  transform: scaleY(1);
  background: white;
}

.menu-icon {
  font-size: 1.25rem;
  width: 1.5rem;
  transition: transform 0.3s ease;
}

.menu-item:hover .menu-icon {
  transform: scale(1.15);
}

.menu-label {
  flex: 1;
  font-size: 0.95rem;
}

.menu-arrow {
  font-size: 0.875rem;
  opacity: 0;
  transform: translateX(-10px);
  transition: all 0.3s ease;
}

.menu-item:hover .menu-arrow {
  opacity: 1;
  transform: translateX(0);
}


.logout-item {
  cursor: pointer;
  margin-top: auto;
  border-top: 1px solid rgba(0, 0, 0, 0.1);
  color: #ef4444;
}

.logout-item:hover {
  background: rgba(239, 68, 68, 0.1);
  color: #dc2626;
}

.logout-item::before {
  background: #ef4444;
}



.body-container {
  position: relative;
  min-height: calc(100vh - 76px);
}


.custom-sidebar {
  position: fixed;
  top: 76px; /* Altura del header */
  left: 0;
  width: 100%;
  height: calc(100vh - 76px);
  background: rgba(0, 0, 0, 0.5);
  z-index: 1000;
  animation: fadeIn 0.3s ease;
}

.sidebar-content {
  width: 320px;
  height: 100%;
  background: #10B981;
  box-shadow: 4px 0 20px rgba(0, 0, 0, 0.06);
  animation: slideIn 0.3s ease;
  overflow-y: auto;
}

.sidebar-header {
  padding: 1.5rem;
  background: linear-gradient(135deg, #34d399 0%, #2dd4bf 100%);
  color: white;
}

.sidebar-brand {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.sidebar-logo {
  height: 45px;
  width: auto;
  object-fit: contain;
}

.brand-info {
  display: flex;
  flex-direction: column;
}

.sidebar-title {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.12);
}

.sidebar-subtitle {
  margin: 0.25rem 0 0 0;
  font-size: 0.875rem;
  opacity: 0.9;
}

.sidebar-menu {
  padding: 1rem 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

/* Animaciones */
@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes slideIn {
  from { transform: translateX(-320px); }
  to { transform: translateX(0); }
}

.main-content {
  padding: 2rem;
  min-height: calc(100vh - 76px);
  transition: all 0.3s ease;
}

.main-content.with-sidebar {
  margin-left: 0; /* No empujar el contenido cuando el sidebar está abierto */
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.fade-enter-from {
  opacity: 0;
  transform: translateY(10px);
}

.fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

@media (max-width: 768px) {
  .welcome-message {
    display: none;
  }

  .user-details {
    display: none;
  }

  .header-end {
    gap: 0.5rem;
  }

  .header-logo {
    height: 32px;
  }
}

@media (max-width: 480px) {
  .custom-toolbar {
    padding: 0.5rem 1rem;
  }

  .header-start {
    gap: 0.5rem;
  }
}
</style>