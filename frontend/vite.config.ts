/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Backend runs on :5080 (see backend launchSettings.json); same-origin in dev, no CORS needed.
    // API_PROXY_TARGET points a second dev server at another backend, e.g. to run checks beside `just dev`.
    proxy: {
      '/api': process.env.API_PROXY_TARGET ?? 'http://localhost:5080',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
  },
})
