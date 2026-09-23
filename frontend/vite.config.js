import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

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
          manualChunks: (id) => {
            if (id.includes('node_modules')) {
              if (id.includes('vue') || id.includes('pinia') || id.includes('@vue')) {
                return 'vendor';
              }
              if (id.includes('primevue') || id.includes('@primeuix')) {
                return 'primevue';
              }
              return 'vendor';
            }
          },
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
