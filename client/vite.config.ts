import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In dev, forward /api calls to the .NET backend.
// `npm run build` outputs into the server's wwwroot so .NET serves the app in production.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: { '/api': 'http://localhost:5067' },
  },
  build: {
    outDir: '../server/wwwroot',
    emptyOutDir: true,
  },
})
