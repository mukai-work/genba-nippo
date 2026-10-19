/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// 開発中は Vite（5192）が画面を出し、/api だけ ASP.NET Core（5290）へ中継する。
// 同一オリジンに見えるので CORS の設定が要らない。
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5192,
    strictPort: true,
    proxy: {
      '/api': 'http://localhost:5290',
    },
  },
  build: {
    // ビルド結果を API の wwwroot に出し、本番は API 1プロセスで画面も配信する
    outDir: '../src/GenbaNippo.Api/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
  },
})
