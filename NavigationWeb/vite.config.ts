import { defineConfig } from 'vite';
export default defineConfig({
  base: './',
  build: { outDir: '../Assets/SolarNavigation', emptyOutDir: true, sourcemap: false },
  server: { host: '127.0.0.1', port: 5186, strictPort: true },
});
