<template>
  <div class="auth-container">
    <!-- Left Side - Form -->
    <div class="auth-form-side">
      <!-- Green Fragment Decoration -->
      <div class="green-fragment"></div>

      <div class="form-content">
        <div class="form-wrapper">
          <div class="text-center mb-4">
            <i class="pi pi-building" style="font-size: 2.5rem; color: #10b981;"></i>
          </div>
          <h2 class="form-title">{{ $t('iam.login.title') }}</h2>
          <p class="form-subtitle">{{ $t('iam.login.subtitle') || 'Welcome back! Please login to your account.' }}</p>

          <form @submit.prevent="handleLogin">
            <!-- Email -->
            <div class="field">
              <label for="email" class="field-label">
                <i class="pi pi-envelope"></i>
                {{ $t('iam.login.email') }}
              </label>
              <pv-input-text
                id="email"
                v-model="loginForm.email"
                type="email"
                :placeholder="$t('iam.login.emailPlaceholder')"
                :invalid="!!fieldErrors.email"
                class="w-full"
              />
              <small v-if="fieldErrors.email" class="p-error block mt-1">{{ fieldErrors.email }}</small>
            </div>

            <!-- Password -->
            <div class="field">
              <label for="password" class="field-label">
                <i class="pi pi-lock"></i>
                {{ $t('iam.login.password') }}
              </label>
              <pv-password
                inputId="password"
                v-model="loginForm.password"
                :placeholder="$t('iam.login.passwordPlaceholder')"
                :invalid="!!fieldErrors.password"
                :feedback="false"
                toggleMask
                class="w-full"
                inputClass="w-full"
              />
              <small v-if="fieldErrors.password" class="p-error block mt-1">{{ fieldErrors.password }}</small>
            </div>

            <!-- Error Message -->
            <pv-message v-if="errorMessage" severity="error" :closable="false" class="mb-3">
              {{ errorMessage }}
            </pv-message>

            <!-- Submit Button -->
            <pv-button
              type="submit"
              :label="$t('iam.login.submitButton')"
              icon="pi pi-sign-in"
              :loading="isLoading"
              class="w-full submit-btn"
            />

            <!-- Register Links -->
            <pv-divider />
            <p class="register-text">{{ $t('iam.login.noAccount') }}</p>
            <div class="flex gap-2">
              <pv-button
                :label="$t('iam.login.registerBuilder')"
                icon="pi pi-user-plus"
                severity="secondary"
                outlined
                @click="goToRegisterBuilder"
                class="flex-1"
              />
              <pv-button
                :label="$t('iam.login.registerOwner')"
                icon="pi pi-user-plus"
                severity="secondary"
                outlined
                @click="goToRegisterOwner"
                class="flex-1"
              />
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Right Side - Image -->
    <div class="auth-image-side"></div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { useIamStore } from '../../application/iam.store.js';
import { isValidEmail } from '../../../shared/presentation/validators.js';

const router = useRouter();
const iamStore = useIamStore();
const { t } = useI18n();

const loginForm = ref({
  email: '',
  password: ''
});

const isLoading = ref(false);
const errorMessage = ref('');
const fieldErrors = ref({});

async function handleLogin() {
  errorMessage.value = '';
  fieldErrors.value = {};

  const email = (loginForm.value.email || '').trim();
  const password = loginForm.value.password || '';

  if (!email) {
    fieldErrors.value.email = t('iam.validation.emailRequired');
  } else if (!isValidEmail(email)) {
    fieldErrors.value.email = t('iam.validation.emailInvalid');
  }

  if (!password) {
    fieldErrors.value.password = t('iam.validation.passwordRequired');
  }

  if (Object.keys(fieldErrors.value).length > 0) {
    return;
  }

  isLoading.value = true;

  try {
    await iamStore.signIn(email, password);
    // Redirect to home or dashboard after successful login
    router.push({ name: 'home' });
  } catch (error) {
    errorMessage.value = t('iam.validation.credentialsInvalid');
  } finally {
    isLoading.value = false;
  }
}

function goToRegisterBuilder() {
  router.push({ name: 'register-builder' });
}

function goToRegisterOwner() {
  router.push({ name: 'register-owner' });
}
</script>

<style scoped>
.auth-container {
  display: flex;
  min-height: 100vh;
  width: 100%;
  background-color: #262626;
  font-family: 'Inter', sans-serif;
}

/* Left Side - Form */
.auth-form-side {
  flex: 1;
  background-color: #18181b;
  background-image: 
      linear-gradient(rgba(255, 255, 255, 0.02) 1px, transparent 1px),
      linear-gradient(90deg, rgba(255, 255, 255, 0.02) 1px, transparent 1px);
  background-size: 40px 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 2rem;
  position: relative;
  z-index: 10;
  overflow: hidden;
}

/* Green Fragment Decoration */
.green-fragment {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 120px;
  height: 300px;
  background: linear-gradient(to top, #10b981, transparent);
  clip-path: polygon(100% 0, 100% 100%, 0% 100%);
  opacity: 0.6;
  z-index: -1;
}

.form-content {
  width: 100%;
  max-width: 480px;
}

@keyframes slideUpFade {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.form-wrapper {
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  padding: 3rem;
  border-radius: 1.5rem;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.1);
  animation: slideUpFade 0.8s cubic-bezier(0.16, 1, 0.3, 1);
}

.form-title {
  font-size: 2rem;
  font-weight: 800;
  background: linear-gradient(135deg, #1f2937 0%, #4b5563 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  margin: 0 0 0.5rem 0;
  text-align: center;
}

.form-subtitle {
  color: #6b7280;
  margin: 0 0 2.5rem 0;
  font-size: 1rem;
  text-align: center;
  line-height: 1.5;
}

.field {
  margin-bottom: 1.5rem;
}

.field-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  color: #374151;
  margin-bottom: 0.5rem;
  font-size: 0.9rem;
  text-transform: uppercase;
  letter-spacing: 0.025em;
}

.field-label i {
  color: #10b981;
}

/* Input Styling */
:deep(.p-inputtext),
:deep(.p-password-input) {
  border-radius: 0.75rem;
  padding: 0.875rem 1rem;
  border-color: #e5e7eb;
  transition: all 0.3s ease;
  background-color: #f9fafb;
}

:deep(.p-inputtext:hover),
:deep(.p-password-input:hover) {
  border-color: #10b981;
  background-color: #fff;
}

:deep(.p-inputtext:focus),
:deep(.p-password-input:focus) {
  box-shadow: 0 0 0 4px rgba(16, 185, 129, 0.1);
  border-color: #10b981;
  background-color: #fff;
}

/* Button Styling */
:deep(.submit-btn) {
  margin-top: 1.5rem;
  height: 3.5rem;
  font-size: 1.1rem;
  font-weight: 600;
  border-radius: 0.75rem;
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
  border: none;
  box-shadow: 0 4px 6px -1px rgba(16, 185, 129, 0.2);
  transition: all 0.3s ease;
}

:deep(.submit-btn:hover) {
  transform: translateY(-2px);
  box-shadow: 0 10px 15px -3px rgba(16, 185, 129, 0.3);
  filter: brightness(1.1);
}

.register-text {
  text-align: center;
  color: #6b7280;
  margin: 1.5rem 0 1rem;
  font-size: 0.95rem;
}

/* Secondary Buttons */
:deep(.p-button.p-button-secondary.p-button-outlined) {
  border-radius: 0.75rem;
  border-width: 2px;
  font-weight: 600;
  transition: all 0.2s;
}

:deep(.p-button.p-button-secondary.p-button-outlined:hover) {
  background-color: #f3f4f6;
  border-color: #4b5563;
  color: #1f2937;
}

/* Right Side - Image */
.auth-image-side {
  flex: 1;
  background-image: url('https://img.freepik.com/free-photo/modern-business-building-with-glass-wall-from-empty-floor_1127-3091.jpg?semt=ais_hybrid&w=740&q=80');
  background-size: cover;
  background-position: center;
  clip-path: polygon(10% 0, 100% 0, 100% 100%, 0% 100%);
}

/* Responsive Design */
@media (max-width: 968px) {
  .auth-container {
    flex-direction: column;
  }

  .auth-image-side {
    min-height: 300px;
    order: -1;
    clip-path: polygon(0 0, 100% 0, 100% 85%, 0 100%);
  }
  
  .green-fragment {
    display: none;
  }
}

@media (max-width: 768px) {
  .auth-image-side {
    display: none;
  }
}

@media (max-width: 640px) {
  .auth-form-side {
    padding: 1rem;
  }

  .form-wrapper {
    padding: 2rem 1.5rem;
  }

  .form-title {
    font-size: 1.75rem;
  }
}
</style>
