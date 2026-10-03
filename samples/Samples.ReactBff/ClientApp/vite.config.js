import { defineConfig } from 'vite';
export default defineConfig({
  base: '/lib/app/',
  build: {
    outDir: '../wwwroot/lib/app',
    emptyOutDir: true,
  },
  server: {
    open: '/lib/app/',
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    // HTTP loopback is for development only; production should serve the built app over HTTPS.
    // Preserve the browser Host so the SDK chooses port 5173 for the loopback OAuth callback.
    proxy: {
      '/api': { target: 'http://127.0.0.1:5254', changeOrigin: false },
      '/oauth': { target: 'http://127.0.0.1:5254', changeOrigin: false },
    },
  },
  test: {
    environment: 'jsdom',
  },
});
