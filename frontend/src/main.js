import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import i18n from "./i18n.js";
import 'primeflex/primeflex.css';
import 'primeicons/primeicons.css';
import PrimeVue from 'primevue/config';
import { iobuildPreset } from './shared/presentation/theme.js';
import {
    Button,
    Card,
    Checkbox,
    Column,
    ConfirmationService,
    ConfirmDialog, DataTable, Dialog,
    DialogService, Divider, Drawer, Fieldset, FileUpload, FloatLabel, IconField, InputIcon, InputNumber, InputText, Menu,
    Message, MultiSelect, Password, ProgressSpinner,
    Rating, Row, Select, SelectButton, Stepper, Step, StepList, StepPanel, StepPanels, Tag, Textarea, Toast,
    ToastService, Toolbar, Tooltip
} from "primevue";
import router from "./router.js";
import pinia from "./pinia.js";

createApp(App)
    .use(i18n)
    .use(PrimeVue, { 
        theme: { 
            preset: iobuildPreset,
            options: {
                darkModeSelector: 'none'
            }
        }, 
        ripple: true
    })
    .use(ConfirmationService)
    .use(DialogService)
    .use(ToastService)
    .component('pv-button', Button)
    .component('pv-card', Card)
    .component('pv-column', Column)
    .component('pv-confirm-dialog', ConfirmDialog)
    .component('pv-checkbox', Checkbox)
    .component('pv-data-table', DataTable)
    .component('pv-dialog', Dialog)
    .component('pv-divider', Divider)
    .component('pv-drawer', Drawer)
    .component('pv-fieldset', Fieldset)
    .component('pv-select', Select)
    .component('pv-select-button', SelectButton)
    .component('pv-stepper', Stepper)
    .component('pv-step', Step)
    .component('pv-step-list', StepList)
    .component('pv-step-panel', StepPanel)
    .component('pv-step-panels', StepPanels)
    .component('pv-file-upload', FileUpload)
    .component('pv-float-label', FloatLabel)
    .component('pv-icon-field', IconField)
    .component('pv-input-icon', InputIcon)
    .component('pv-input-number', InputNumber)
    .component('pv-input-text', InputText)
    .component('pv-menu', Menu)
    .component('pv-message', Message)
    .component('pv-multi-select', MultiSelect)
    .component('pv-password', Password)
    .component('pv-progress-spinner', ProgressSpinner)
    .component('pv-rating', Rating)
    .component('pv-row', Row)
    .component('pv-tag', Tag)
    .component('pv-textarea', Textarea)
    .component('pv-toast', Toast)
    .component('pv-toolbar', Toolbar)
    .component('pv-tooltip', Tooltip)
    .use(router)
    .use(pinia)
    .mount('#app')
