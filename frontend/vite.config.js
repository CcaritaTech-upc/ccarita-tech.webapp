import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

export function vendorChunk(id) {
  const path = id.replaceAll('\\', '/');
  if (!path.includes('/node_modules/')) return;
  if (/\/node_modules\/(chart\.js|vue-chartjs|@kurkle\/color)\//.test(path)) return 'charts';
  if (path.includes('/node_modules/@stripe/')) return 'stripe';
  // Match package boundaries: primevue and vue-chartjs are not Vue core.
  if (/\/node_modules\/(primevue|@primevue\/[^/]+|@primeuix\/[^/]+)\//.test(path)) return 'primevue';
  return 'vendor';
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  return {
    plugins: [vue()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    build: {
      rollupOptions: {
        output: {
          onlyExplicitManualChunks: true,
          manualChunks: vendorChunk,
        },
      },
      chunkSizeWarningLimit: 1000,
    },
    server: {
      fs: {
        caseSensitive: true,
      },
      proxy: {
        '/api': {
          target: env.VITE_PROXY_TARGET || process.env.VITE_PROXY_TARGET || 'http://localhost:5080',
          changeOrigin: true,
        },
      },
    },
  };
})
