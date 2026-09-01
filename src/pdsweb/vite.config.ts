import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  base: '/',
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../atompds/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/xrpc': 'http://localhost:5093',
      '/api/admin': 'http://localhost:5093',
      '/api/pending': 'http://localhost:5093',
    },
  },
})