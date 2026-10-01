<template>
  <div class="auth-container">
    <!-- Left Side - Form -->
    <div class="auth-form-side">
      <!-- Green Fragment Decoration -->
      <div class="green-fragment"></div>

      <div class="form-content">
        <div class="form-wrapper">
          <h2 class="form-title">{{ $t('iam.registerOwner.title') }}</h2>
          <p class="form-subtitle">{{ $t('iam.registerOwner.subtitle') || 'Create your owner account to manage your properties.' }}</p>

          <pv-stepper v-model:value="currentStep" linear role="group">
            <pv-step-list role="tablist" :aria-label="$t('iam.registerOwner.title')">
              <pv-step :value="1" :pt="{ root: { 'aria-current': null }, header: { 'aria-selected': currentStep === 1 } }">{{ $t('iam.registerOwner.userInfoSection') }}</pv-step>
              <pv-step :value="2" :pt="{ root: { 'aria-current': null }, header: { 'aria-selected': currentStep === 2 } }">{{ $t('iam.registerOwner.profileInfoSection') }}</pv-step>
            </pv-step-list>

            <pv-step-panels>
              <!-- Step 1: Account Information -->
              <pv-step-panel :value="1">
                <div class="step-content">
          <!-- Email -->
          <div class="mb-3">
            <label for="email" class="block mb-2">{{ $t('iam.registerOwner.email') }} *</label>
            <pv-input-text
              id="email"
              v-model="registerForm.email"
              type="email"
              :placeholder="$t('iam.registerOwner.emailPlaceholder')"
              :invalid="!!fieldErrors.email"
              class="w-full"
              @input="onOwnerEmailInput"
              @blur="onEmailBlur"
            />
            <small v-if="fieldErrors.email" class="p-error block mt-1">
              {{ fieldErrors.email }}
              <a v-if="isEmailAlreadyRegistered" href="#" @click.prevent="goToLogin" class="text-green-500 font-semibold underline ml-1">{{ $t('iam.login.submitButton') }}</a>
            </small>
          </div>

          <!-- Password -->
          <div class="mb-3">
            <label for="password" class="block mb-2">{{ $t('iam.registerOwner.password') }} *</label>
            <pv-password
              inputId="password"
              v-model="registerForm.password"
              :placeholder="$t('iam.registerOwner.passwordPlaceholder')"
              :invalid="!!fieldErrors.password"
              toggleMask
              class="w-full"
              inputClass="w-full"
            />
            <small v-if="fieldErrors.password" class="p-error block mt-1">{{ fieldErrors.password }}</small>
          </div>

          <!-- Confirm Password -->
          <div class="mb-3">
            <label for="confirmPassword" class="block mb-2">{{ $t('iam.registerOwner.confirmPassword') }} *</label>
            <pv-password
              inputId="confirmPassword"
              v-model="registerForm.confirmPassword"
              :placeholder="$t('iam.registerOwner.confirmPasswordPlaceholder')"
              :invalid="!!fieldErrors.confirmPassword"
              :feedback="false"
              toggleMask
              class="w-full"
              inputClass="w-full"
            />
            <small v-if="fieldErrors.confirmPassword" class="p-error block mt-1">{{ fieldErrors.confirmPassword }}</small>
          </div>

          <!-- Error Message -->
          <pv-message v-if="errorMessage" severity="error" :closable="false" class="mb-3">
            {{ errorMessage }}
          </pv-message>

                <div class="flex gap-2 pt-4">
                  <pv-button
                    type="button"
                    :label="$t('iam.registerOwner.cancelButton')"
                    severity="secondary"
                    outlined
                    @click="goToLogin"
                    class="flex-1"
                  />
                  <pv-button
                    :label="$t('iam.actions.next')"
                    icon="pi pi-arrow-right"
                    iconPos="right"
                    :loading="checkingInvitation"
                    :disabled="checkingInvitation || ownerUnitAssigned === false"
                    :class="{ 'owner-next-button--unavailable': ownerUnitAssigned === false }"
                    @click="goToStep2"
                    class="flex-1"
                  />
                </div>
              </div>
            </pv-step-panel>

            <!-- Step 2: Personal Information -->
            <pv-step-panel :value="2">
              <div class="step-content">
                <!-- Builder assignment detection banner -->
                <pv-message v-if="invitationInfo" severity="success" :closable="false" class="mb-4">
                  <div class="flex items-center gap-2">
                    <i class="pi pi-check-circle" style="font-size: 1.1rem;"></i>
                    <span>
                      {{ $t('iam.messages.ownerAssignmentDetected') }}
                    </span>
                  </div>
                </pv-message>

          <!-- Photo URL -->
          <div class="mb-3">
            <label for="photoUrl" class="block mb-2">{{ $t('iam.registerOwner.photoUrl') }}</label>
            <input
              type="file"
              ref="fileInput"
              accept="image/*"
              style="display: none;"
              @change="handleLocalFileUpload"
            />
            <pv-button
              type="button"
              :label="registerForm.photoUrl ? $t('iam.actions.changePhoto') : $t('iam.registerOwner.uploadPhoto')"
              icon="pi pi-cloud-upload"
              @click="openUploadModal"
              severity="secondary"
              outlined
              class="w-full mb-2"
            />
            <div v-if="registerForm.photoUrl" class="mt-2 text-center">
              <img
                :src="registerForm.photoUrl"
                :alt="$t('iam.actions.photoPreviewAlt')"
                class="uploaded-image"
              />
              <div class="flex justify-content-center align-items-center gap-2 mt-2">
                <span class="text-sm text-green-600 font-medium">✓ {{ $t('iam.actions.photoSelected') }}</span>
                <pv-button
                  type="button"
                  icon="pi pi-trash"
                  text
                  rounded
                  severity="danger"
                  size="small"
                  @click="registerForm.photoUrl = ''"
                  :title="$t('iam.actions.removePhoto')"
                />
              </div>
            </div>
          </div>

          <!-- Name -->
          <div class="mb-3">
            <label for="name" class="block mb-2">{{ $t('iam.registerOwner.name') }} *</label>
            <pv-input-text
              id="name"
              v-model="registerForm.name"
              :placeholder="$t('iam.registerOwner.namePlaceholder')"
              :invalid="!!fieldErrors.name"
              class="w-full"
            />
            <small v-if="fieldErrors.name" class="p-error block mt-1">{{ fieldErrors.name }}</small>
          </div>

          <!-- Username -->
          <div class="mb-3">
            <label for="username" class="block mb-2">{{ $t('iam.registerOwner.username') }} *</label>
            <pv-input-text
              id="username"
              v-model="registerForm.username"
              :placeholder="$t('iam.registerOwner.usernamePlaceholder')"
              :invalid="!!fieldErrors.username"
              class="w-full"
            />
            <small v-if="fieldErrors.username" class="p-error block mt-1">{{ fieldErrors.username }}</small>
          </div>

          <!-- Address -->
          <div class="mb-3">
            <label for="address" class="block mb-2">{{ $t('iam.registerOwner.address') }} *</label>
            <pv-input-text
              id="address"
              v-model="registerForm.address"
              :placeholder="$t('iam.registerOwner.addressPlaceholder')"
              :invalid="!!fieldErrors.address"
              class="w-full"
            />
            <small v-if="fieldErrors.address" class="p-error block mt-1">{{ fieldErrors.address }}</small>
          </div>

          <!-- Age -->
          <div class="mb-3">
            <label for="age" class="block mb-2">{{ $t('iam.registerOwner.age') }} *</label>
            <pv-input-number
              id="age"
              v-model="registerForm.age"
              :min="18"
              :max="120"
              :placeholder="$t('iam.registerOwner.agePlaceholder')"
              :invalid="!!fieldErrors.age"
              class="w-full"
            />
            <small v-if="fieldErrors.age" class="p-error block mt-1">{{ fieldErrors.age }}</small>
          </div>

          <!-- Phone Number -->
          <div class="mb-3">
            <label for="phoneNumber" class="block mb-2">{{ $t('iam.registerOwner.phoneNumber') }} *</label>
            <pv-input-text
              id="phoneNumber"
              v-model="registerForm.phoneNumber"
              :placeholder="$t('iam.registerOwner.phoneNumberPlaceholder')"
              :invalid="!!fieldErrors.phoneNumber"
              class="w-full"
            />
            <small v-if="fieldErrors.phoneNumber" class="p-error block mt-1">{{ fieldErrors.phoneNumber }}</small>
          </div>

          <!-- Error/Success Messages -->
          <pv-message v-if="errorMessage" severity="error" :closable="false" class="mb-3">
            {{ errorMessage }}
          </pv-message>

          <pv-message v-if="successMessage" severity="success" :closable="false" class="mb-3">
            {{ successMessage }}
          </pv-message>

                <div class="flex gap-2 pt-4">
                  <pv-button
                    :label="$t('iam.actions.back')"
                    severity="secondary"
                    icon="pi pi-arrow-left"
                    @click="currentStep = 1"
                    class="flex-1"
                  />
                  <pv-button
                    :label="$t('iam.registerOwner.submitButton')"
                    icon="pi pi-check"
                    :loading="isLoading"
                    @click="handleRegister"
                    class="flex-1"
                  />
                </div>
              </div>
            </pv-step-panel>
          </pv-step-panels>
        </pv-stepper>
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
import { useProfileStore } from '../../../profiles/application/profile.store.js';
import { IamApi } from '../../infrastructure/iam-api.js';
import {
  isValidEmail,
  isValidPhone,
  isValidAge,
  isValidName,
  isValidUsername,
  isValidPassword
} from '../../../shared/presentation/validators.js';

const router = useRouter();
const { t } = useI18n();
const iamStore = useIamStore();
const profileStore = useProfileStore();
const iamApi = new IamApi();

const currentStep = ref(1);
const fieldErrors = ref({});

import { loadCloudinaryWidget } from "../../../shared/infrastructure/cloudinary-loader.js";
import { getAvatarUploadConfig } from "../../../shared/infrastructure/cloudinary-config.js";

// Cloudinary configuration
const cloudinaryName = import.meta.env.VITE_CLOUDINARY_CLOUD_NAME;
const cloudinaryPreset = import.meta.env.VITE_CLOUDINARY_UPLOAD_PRESET;
let openingUpload = false;

const fileInput = ref(null);

const handleLocalFileUpload = (event) => {
  const file = event.target.files?.[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = (e) => {
    registerForm.value.photoUrl = e.target.result;
  };
  reader.readAsDataURL(file);
};

const openUploadModal = async () => {
  if (openingUpload) return;
  const hasCloudinary = cloudinaryName && cloudinaryName.trim() !== '' && cloudinaryPreset && cloudinaryPreset.trim() !== '';
  if (hasCloudinary) {
    openingUpload = true;
    try {
      const cloudinary = await loadCloudinaryWidget();
      const widgetConfig = getAvatarUploadConfig(cloudinaryName, cloudinaryPreset);
      cloudinary.openUploadWidget(
        widgetConfig,
        (error, result) => {
          if (!error && result && result.event === "success") {
            registerForm.value.photoUrl = result.info.secure_url || result.info.url;
          }
        }
      );
      return;
    } catch (err) {
      console.warn('Cloudinary widget failed, using local file picker:', err);
    } finally {
      openingUpload = false;
    }
  }

  // Fallback seguro: abrir selector de archivo nativo sin depender de Cloudinary
  if (fileInput.value) {
    fileInput.value.click();
  }
};

const registerForm = ref({
  email: '',
  password: '',
  confirmPassword: '',
  photoUrl: '',
  name: '',
  username: '',
  address: '',
  age: null,
  phoneNumber: ''
});

const isLoading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');
const invitationInfo = ref(null);
const checkingInvitation = ref(false);
const isEmailAlreadyRegistered = ref(false);
const ownerUnitAssigned = ref(null);

function onOwnerEmailInput() {
  ownerUnitAssigned.value = null;
  invitationInfo.value = null;
  isEmailAlreadyRegistered.value = false;

  if (fieldErrors.value.email === t('iam.validation.ownerUnitRequired') ||
      fieldErrors.value.email === t('iam.validation.emailAlreadyRegistered')) {
    fieldErrors.value.email = null;
  }
  if (errorMessage.value === t('iam.validation.ownerUnitRequired') ||
      errorMessage.value === t('iam.validation.emailAlreadyRegistered') ||
      errorMessage.value === t('iam.validation.ownerUnitLookupFailed')) {
    errorMessage.value = '';
  }
}

async function checkOwnerUnitAssignment(email) {
  if (!email || !email.includes('@')) return false;
  try {
    checkingInvitation.value = true;
    const res = await iamApi.checkInvitation(email.trim());
    if (res?.data?.alreadyRegistered) {
      isEmailAlreadyRegistered.value = true;
      ownerUnitAssigned.value = false;
      invitationInfo.value = null;
      fieldErrors.value.email = t('iam.validation.emailAlreadyRegistered');
      errorMessage.value = t('iam.validation.emailAlreadyRegistered');
      return false;
    }
    isEmailAlreadyRegistered.value = false;
    if (!res?.data?.assigned) {
      ownerUnitAssigned.value = false;
      invitationInfo.value = null;
      fieldErrors.value.email = t('iam.validation.ownerUnitRequired');
      errorMessage.value = t('iam.validation.ownerUnitRequired');
      return false;
    }
    ownerUnitAssigned.value = true;
    if (fieldErrors.value.email === t('iam.validation.ownerUnitRequired') ||
        fieldErrors.value.email === t('iam.validation.emailAlreadyRegistered')) {
      fieldErrors.value.email = null;
    }
    if (errorMessage.value === t('iam.validation.ownerUnitRequired') ||
        errorMessage.value === t('iam.validation.emailAlreadyRegistered')) {
      errorMessage.value = '';
    }
    invitationInfo.value = { assigned: true };
    return true;
  } catch (err) {
    console.debug('Invitation lookup failed or error:', err);
    ownerUnitAssigned.value = false;
    invitationInfo.value = null;
    errorMessage.value = t('iam.validation.ownerUnitLookupFailed');
    return false;
  } finally {
    checkingInvitation.value = false;
  }
}

async function onEmailBlur() {
  const email = (registerForm.value.email || '').trim();
  if (email && isValidEmail(email)) {
    await checkOwnerUnitAssignment(email);
  }
}

async function goToStep2() {
  errorMessage.value = '';
  fieldErrors.value = {};

  const email = (registerForm.value.email || '').trim();
  const password = registerForm.value.password || '';
  const confirmPassword = registerForm.value.confirmPassword || '';

  if (!email) {
    fieldErrors.value.email = t('iam.validation.emailRequired');
  } else if (!isValidEmail(email)) {
    fieldErrors.value.email = t('iam.validation.emailInvalid');
  }

  if (!password) {
    fieldErrors.value.password = t('iam.validation.passwordRequired');
  } else if (!isValidPassword(password, 8)) {
    fieldErrors.value.password = t('iam.validation.passwordMinLength');
  }

  if (!confirmPassword) {
    fieldErrors.value.confirmPassword = t('iam.validation.confirmPasswordRequired');
  } else if (password !== confirmPassword) {
    fieldErrors.value.confirmPassword = t('iam.validation.passwordMismatch');
  }

  if (Object.keys(fieldErrors.value).length > 0) {
    errorMessage.value = t('iam.validation.completeAccountInfo');
    return;
  }

  // Check if email already registered before allowing step 2
  const allowed = await checkOwnerUnitAssignment(email);
  if (!allowed || isEmailAlreadyRegistered.value) {
    return;
  }

  registerForm.value.email = email;
  currentStep.value = 2;
}

async function handleRegister() {
  isLoading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  fieldErrors.value = {};

  const name = (registerForm.value.name || '').trim();
  const username = (registerForm.value.username || '').trim();
  const address = (registerForm.value.address || '').trim();
  const age = registerForm.value.age;
  const phoneNumber = (registerForm.value.phoneNumber || '').trim();

  if (!name) {
    fieldErrors.value.name = t('iam.validation.fullNameRequired');
  } else if (!isValidName(name, 2)) {
    fieldErrors.value.name = t('iam.validation.fullNameMinLength');
  }

  if (!username) {
    fieldErrors.value.username = t('iam.validation.usernameRequired');
  } else if (!isValidUsername(username)) {
    fieldErrors.value.username = t('iam.validation.usernameInvalid');
  }

  if (!address) {
    fieldErrors.value.address = t('iam.validation.addressRequired');
  } else if (address.length < 4) {
    fieldErrors.value.address = t('iam.validation.addressMinLength');
  }

  if (age === null || age === undefined || age === '') {
    fieldErrors.value.age = t('iam.validation.ageRequired');
  } else if (!isValidAge(age, 18, 120)) {
    fieldErrors.value.age = t('iam.validation.ageInvalid');
  }

  if (!phoneNumber) {
    fieldErrors.value.phoneNumber = t('iam.validation.phoneRequired');
  } else if (!isValidPhone(phoneNumber)) {
    fieldErrors.value.phoneNumber = t('iam.validation.phoneInvalid');
  }

  if (Object.keys(fieldErrors.value).length > 0) {
    errorMessage.value = t('iam.validation.completeProfileInfo');
    isLoading.value = false;
    return;
  }

  try {
    // 1. Create user with role "Owner"
    console.log('Step 1: Creating user account...');
    await iamStore.signUp({
      email: registerForm.value.email,
      password: registerForm.value.password,
      role: 'Owner'
    });
    console.log('Step 1: User account created successfully');

    // 2. Sign in to get the user ID
    console.log('Step 2: Signing in to get user ID...');
    const authenticatedUser = await iamStore.signIn(registerForm.value.email, registerForm.value.password);
    console.log('Step 2: Sign in successful, user ID:', authenticatedUser.id);

    // Validate that we have a user ID
    if (!authenticatedUser || !authenticatedUser.id) {
      throw new Error('Failed to retrieve user ID after sign in');
    }

    // 3. Create profile with user ID
    console.log('Step 3: Creating profile for user ID:', authenticatedUser.id);
    const profileData = {
      userId: authenticatedUser.id,
      photoUrl: registerForm.value.photoUrl || '',
      name: registerForm.value.name,
      username: registerForm.value.username,
      address: registerForm.value.address,
      age: registerForm.value.age ? parseInt(registerForm.value.age) : null,
      phoneNumber: registerForm.value.phoneNumber,
      secondEmail: registerForm.value.secondEmail || ''
    };
    console.log('Profile data to be created:', profileData);
    
    await profileStore.createProfile(profileData);
    console.log('Step 3: Profile created successfully');

    // Immediately update IAM user in store and localStorage so layout header reflects name and photo
    iamStore.updateUserProfile({
      username: profileData.username,
      name: profileData.name,
      photoUrl: profileData.photoUrl
    });

    successMessage.value = t('iam.messages.registrationSuccess');
    
    // Redirect to home after 2 seconds
    setTimeout(() => {
      router.push({ name: 'home' });
    }, 2000);

  } catch (error) {
    console.error('Registration error:', error);
    console.error('Error details:', {
      message: error.message,
      response: error.response?.data,
      status: error.response?.status
    });
    
    // Provide more specific error messages
    if (error.response?.data?.code === 'owner_unit_assignment_required') {
      errorMessage.value = t('iam.validation.ownerUnitRequired');
      fieldErrors.value.email = t('iam.validation.ownerUnitRequired');
      currentStep.value = 1;
    } else if (error.message.includes('user ID')) {
      errorMessage.value = t('iam.messages.profileCreationFailed');
    } else if (error.response?.status === 409 || error.response?.data?.error?.includes('already exists')) {
      errorMessage.value = t('iam.validation.emailAlreadyRegistered');
      fieldErrors.value.email = t('iam.validation.emailAlreadyRegistered');
      isEmailAlreadyRegistered.value = true;
      currentStep.value = 1;
    } else {
      const apiMessage = error.response?.data?.message || error.response?.data?.error;
      errorMessage.value = apiMessage === 'Invalid registration data.'
        ? t('iam.validation.registrationDataInvalid')
        : t('iam.validation.registrationFailed');
    }
  } finally {
    isLoading.value = false;
  }
}

function goToLogin() {
  router.push({ name: 'login' });
}
</script>

<style scoped>
.auth-container {
  display: flex;
  min-height: 100vh;
  width: 100%;
  background-color: #262626;
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
  overflow-y: auto;
  position: relative;
  z-index: 10;
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
  max-width: 550px;
}

.form-wrapper {
  background: white;
  padding: 2rem;
  border-radius: 1rem;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
}

.form-title {
  font-size: 1.875rem;
  font-weight: 700;
  color: #1f2937;
  margin: 0 0 0.5rem 0;
}

.form-subtitle {
  color: #6b7280;
  margin: 0 0 1.5rem 0;
  font-size: 0.95rem;
}

.step-content {
  padding: 1rem 0;
}

.owner-next-button--unavailable:disabled {
  background-color: #9ca3af;
  border-color: #9ca3af;
  color: #ffffff;
  cursor: not-allowed;
  opacity: 1;
}

.mb-3 {
  margin-bottom: 1.25rem;
}

.uploaded-image {
  max-width: 200px;
  max-height: 200px;
  border-radius: 8px;
  border: 2px solid #10b981;
  object-fit: cover;
  margin: 0 auto;
  display: block;
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

@media (max-width: 640px) {
  .auth-form-side {
    padding: 1rem;
  }

  .form-wrapper {
    padding: 1.5rem;
  }

  .form-title {
    font-size: 1.5rem;
  }
}
</style>
