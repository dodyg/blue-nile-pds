import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  base: '/',
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../atompds/wwwroot',
    emptyOutDir: false,
  },
  server: {
    proxy: {
      '/xrpc': 'http://localhost:5093',
    },
  },
})