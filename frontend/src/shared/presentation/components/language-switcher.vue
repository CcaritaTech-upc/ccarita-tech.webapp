<script setup>
import {useI18n} from "vue-i18n";
import { computed, ref, watch} from "vue";
import { LANG_KEY } from "../../infrastructure/storage-keys.js";

const { t, locale, availableLocales } = useI18n();

const languageOptions = computed(() => {
  return availableLocales.map(lang => ({
    label: lang === 'en' ? 'EN' : 'ES',
    value: lang
  }));
});
const selectRef = ref(null);

watch(locale, (newLang) => {
  localStorage.setItem(LANG_KEY, newLang);
});

</script>

<template>
  <div class="language-select-wrapper">
    <pv-select
      ref="selectRef"
      v-model="locale"
      :options="languageOptions"
      option-label="label"
      option-value="value"
      class="language-select custom-green-select"
      placeholder="Idioma"
      :aria-label="t('profile.appLanguage')"
    />
  </div>
</template>

<style>
</style>
