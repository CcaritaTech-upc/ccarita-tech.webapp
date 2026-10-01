<template>
  <div class="profile-container">
    <h1 class="profile-title">{{ $t('profile.title') }}</h1>

    <div class="profile-header">
      <div class="profile-card">
        <div class="photo-wrapper">
          <img
            v-if="profile.photoUrl"
            :src="profile.photoUrl"
            alt="Profile Photo"
            class="profile-photo"
          />
          <div v-else class="profile-photo placeholder">
            <span>{{ profile.name ? profile.name.charAt(0).toUpperCase() : 'B' }}</span>
          </div>
          <div v-if="isEditing" class="photo-upload-overlay">
            <input
              type="file"
              ref="fileInput"
              accept="image/*"
              style="display: none;"
              @change="handlePhotoChange"
            />
            <pv-button
              type="button"
              icon="pi pi-camera"
              class="change-photo-btn"
              rounded
              @click="triggerPhotoUpload"
              title="Cambiar foto"
              aria-label="Cambiar foto"
            />
          </div>
        </div>
        <div class="profile-info">
          <h2 class="profile-name">{{ profile.name }}</h2>
          <p class="profile-role">{{ $t('profile.builderRole') }}</p>
        </div>

        <div class="edit-buttons">
          <pv-button
            v-if="!isEditing"
            :label="$t('profile.edit')"
            class="edit-button"
            @click="toggleEdit"
          />
          <div v-else class="edit-actions">
            <pv-button
              :label="$t('profile.save')"
              class="edit-button"
              @click="toggleEdit"
            />
            <pv-button
              :label="$t('profile.cancel')"
              class="cancel-button"
              @click="cancelEdit"
            />
          </div>
        </div>
      </div>
    </div>

    <div class="profile-body">
      <div class="account-card">
        <h3 class="card-title">{{ $t('profile.accountInformation') }}</h3>

        <div class="info-group">
          <label for="builder-profile-name">{{ $t('profile.fullName') }}</label>
          <input
            id="builder-profile-name"
            type="text"
            v-model="profile.name"
            :readonly="!isEditing"
            :class="['info-input', { 'input-error': isEditing && errors.name }]"
            @input="errors.name = ''"
          />
          <small v-if="isEditing && errors.name" class="p-error">{{ errors.name }}</small>
        </div>

        <div class="info-group">
          <label for="builder-profile-email">{{ $t('profile.email') }}</label>
          <input id="builder-profile-email" type="text" v-model="profile.email" readonly class="info-input" />
        </div>

        <div class="info-group">
          <label for="builder-profile-phone">{{ $t('profile.phoneNumber') }}</label>
          <input
            id="builder-profile-phone"
            type="text"
            v-model="profile.phoneNumber"
            :readonly="!isEditing"
            :class="['info-input', { 'input-error': isEditing && errors.phoneNumber }]"
            @input="errors.phoneNumber = ''"
          />
          <small v-if="isEditing && errors.phoneNumber" class="p-error">{{ errors.phoneNumber }}</small>
        </div>

        <div class="info-group">
          <label for="builder-profile-address">{{ $t('profile.address') }}</label>
          <input
            id="builder-profile-address"
            type="text"
            v-model="profile.address"
            :readonly="!isEditing"
            :class="['info-input', { 'input-error': isEditing && errors.address }]"
            @input="errors.address = ''"
          />
          <small v-if="isEditing && errors.address" class="p-error">{{ errors.address }}</small>
        </div>

        <div class="info-group">
          <label for="builder-profile-second-email">{{ $t('profile.secondEmail') }}</label>
          <input
            id="builder-profile-second-email"
            type="email"
            v-model="profile.secondEmail"
            :readonly="!isEditing"
            :class="['info-input', { 'input-error': isEditing && errors.secondEmail }]"
            @input="errors.secondEmail = ''"
          />
          <small v-if="isEditing && errors.secondEmail" class="p-error">{{ errors.secondEmail }}</small>
        </div>

        <div class="info-group">
          <label for="builder-profile-years-in-business">{{ $t('profile.yearsInBusiness') }}</label>
          <input
            id="builder-profile-years-in-business"
            type="number"
            min="0"
            max="120"
            step="1"
            v-model.number="profile.yearsInBusiness"
            :readonly="!isEditing"
            :class="['info-input', { 'input-error': isEditing && errors.yearsInBusiness }]"
            @input="errors.yearsInBusiness = ''"
          />
          <small v-if="isEditing && errors.yearsInBusiness" class="p-error">{{ errors.yearsInBusiness }}</small>
        </div>

        <h3 id="builder-profile-language-label" class="card-title">{{ $t('profile.appLanguage') }}</h3>
        <select aria-labelledby="builder-profile-language-label" v-model="$i18n.locale" class="language-select">
          <option value="es">Español</option>
          <option value="en">English</option>
        </select>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { useProfileStore } from '../../application/profile.store.js'
import { ProfileApi } from '../../infrastructure/profile-api.js'
import { isValidName, isValidPhone, isValidEmail, isValidYearsInBusiness } from '../../../shared/presentation/validators.js'
import PvButton from 'primevue/button'

const { t } = useI18n()
const store = useProfileStore()
const profile = computed(() => store.profile)
const api = new ProfileApi()
const isEditing = ref(false)
const fileInput = ref(null)

const errors = reactive({
  name: '',
  phoneNumber: '',
  address: '',
  secondEmail: '',
  yearsInBusiness: ''
})

function validate() {
  errors.name = ''
  errors.phoneNumber = ''
  errors.address = ''
  errors.secondEmail = ''
  errors.yearsInBusiness = ''
  let valid = true

  if (!isValidName(profile.value.name, 2)) {
    errors.name = 'El nombre completo debe tener al menos 2 caracteres.'
    valid = false
  }

  if (profile.value.phoneNumber && !isValidPhone(profile.value.phoneNumber)) {
    errors.phoneNumber = 'Número de teléfono inválido (debe contener entre 7 y 15 dígitos).'
    valid = false
  }

  if (profile.value.address && profile.value.address.trim().length < 4) {
    errors.address = 'La dirección debe tener al menos 4 caracteres.'
    valid = false
  }

  if (profile.value.secondEmail && !isValidEmail(profile.value.secondEmail)) {
    errors.secondEmail = 'Formato de correo secundario no válido.'
    valid = false
  }

  const yearsInBusiness = profile.value.yearsInBusiness
  if (yearsInBusiness !== null && yearsInBusiness !== undefined && yearsInBusiness !== '' && !isValidYearsInBusiness(yearsInBusiness)) {
    errors.yearsInBusiness = t('iam.validation.yearsBusinessInvalid')
    valid = false
  }

  return valid
}

function triggerPhotoUpload() {
  if (fileInput.value) {
    fileInput.value.click()
  }
}

function handlePhotoChange(event) {
  const file = event.target.files?.[0]
  if (!file) return

  const reader = new FileReader()
  reader.onload = (e) => {
    profile.value.photoUrl = e.target.result
  }
  reader.readAsDataURL(file)
}

async function toggleEdit() {
  if (isEditing.value) {
    if (!validate()) {
      return
    }
    await saveProfile()
    isEditing.value = false
  } else {
    errors.name = ''
    errors.phoneNumber = ''
    errors.address = ''
    errors.secondEmail = ''
    errors.yearsInBusiness = ''
    isEditing.value = true
  }
}

async function saveProfile() {
  try {
    // Only send fields that belong to profiles bounded context
    // Email and role are not part of profiles, they belong to IAM
    await api.updateProfile(profile.value.id, {
      id: profile.value.id,
      userId: profile.value.userId,
      name: profile.value.name,
      username: profile.value.username,
      address: profile.value.address,
      phoneNumber: profile.value.phoneNumber,
      photoUrl: profile.value.photoUrl,
      secondEmail: profile.value.secondEmail,
      yearsInBusiness: profile.value.yearsInBusiness === '' ? null : profile.value.yearsInBusiness
    })
    console.log('Profile updated successfully')
    if (profile.value?.userId) {
      await store.fetchProfile(profile.value.userId)
    }
  } catch (error) {
    console.error('Error updating profile:', error)
  }
}

function cancelEdit() {
  isEditing.value = false
  errors.name = ''
  errors.phoneNumber = ''
  errors.address = ''
  errors.secondEmail = ''
  errors.yearsInBusiness = ''
  // Use userId from profile, not profile.id (which is the profile's ID, not user's ID)
  if (profile.value?.userId) {
    store.fetchProfile(profile.value.userId)
  }
}
</script>

<style scoped>
* {
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}
.profile-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 20px;
  padding: 2rem;
  background-color: #f8fafc;
}
.profile-title {
  text-align: left;
  font-size: 1.8rem;
  font-weight: 700;
  color: #111827;
}
.profile-header {
  display: flex;
  justify-content: center;
  width: 100%;
}
.profile-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  background-color: #ecfdf5;
  border-radius: 10px;
  border: 1px solid black;
  padding: 1.5rem 2rem;
  width: 100%;
  max-width: 800px;
}
.photo-wrapper {
  position: relative;
  width: 120px;
  height: 120px;
  flex-shrink: 0;
}
.profile-photo {
  width: 120px;
  height: 120px;
  border-radius: 50%;
  border: 2px solid black;
  object-fit: cover;
}
.profile-photo.placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2.5rem;
  font-weight: 700;
  color: white;
  background-color: #10b981;
}
.photo-upload-overlay {
  position: absolute;
  bottom: 0;
  right: 0;
}
.change-photo-btn {
  background-color: #047857 !important;
  color: white !important;
  border: 2px solid white !important;
  width: 36px !important;
  height: 36px !important;
}
.profile-info {
  flex-grow: 1;
}
.profile-name {
  font-size: 1.4rem;
  font-weight: 600;
  color: #000;
}
.profile-role {
  font-size: 0.95rem;
  color: #475569;
}
.edit-buttons,
.edit-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}
.edit-button {
  background-color: #047857;
  color: white;
  border: none;
  font-weight: bold;
}
.cancel-button {
  background-color: #b91c1c;
  color: white;
  border: none;
  font-weight: bold;
}
.profile-body {
  width: 100%;
  display: flex;
  justify-content: center;
}
.account-card {
  background: white;
  border-radius: 10px;
  border: 1px solid black;
  padding: 25px;
  width: 100%;
  max-width: 800px;
}
.card-title {
  font-size: 1.25rem;
  font-weight: 600;
  color: #111827;
  border-bottom: 1px solid black;
  margin-bottom: 20px;
}
.info-group {
  margin-bottom: 15px;
}
.info-group label {
  font-weight: 500;
  color: #111827;
  margin-bottom: 5px;
  display: block;
}
.info-input {
  width: 100%;
  background-color: #ecfdf5;
  border: 1px solid black;
  border-radius: 6px;
  padding: 0.6rem 0.8rem;
}
.info-input.input-error {
  border-color: #ef4444 !important;
  background-color: #fef2f2 !important;
}
.p-error {
  color: #ef4444;
  font-size: 0.85rem;
  margin-top: 0.25rem;
  display: block;
}
.language-select {
  margin-top: 10px;
  padding: 8px 10px;
  border: 1px solid black;
  border-radius: 6px;
  width: 100%;
}
</style>
