import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  base: '/admin/',
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../atompds/wwwroot/admin',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/xrpc': 'http://localhost:5093',
    },
  },
})
