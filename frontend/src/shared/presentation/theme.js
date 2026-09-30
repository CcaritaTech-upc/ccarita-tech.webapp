import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

// Keep the existing emerald palette; darken interactive text/fills on white.
export const iobuildPreset = definePreset(Aura, {
  semantic: {
    primary: {
      500: '#047857',
      600: '#065f46',
      700: '#064e3b',
    },
  },
});
