/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const api = 'http://localhost:5201';

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, proxy: { '/api': api, '/auth': api, '/audit': api } },
  test: { environment: 'node' },
});
