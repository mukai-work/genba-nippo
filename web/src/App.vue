<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <header class="app-header">
    <div class="app-header-inner">
      <RouterLink to="/" class="brand">現場日報ボード</RouterLink>
      <span class="brand-sub">作業日報・工程進捗・承認</span>
      <span class="demo-note" title="認証・権限は対象外です。デモのため、日報の作成も承認も同じ画面で操作できます。">デモ版：全操作が可能</span>
    </div>
  </header>
  <RouterView />
</template>

<style scoped>
.app-header { background: var(--navy); color: #fff; }
.app-header-inner { max-width: 1120px; margin: 0 auto; padding: 0 16px; height: 52px; display: flex; align-items: center; gap: 16px; }
.brand { color: #fff; font-weight: 700; font-size: 16px; }
.brand:hover { text-decoration: none; }
.brand-sub { color: #9fb3c8; font-size: 12px; }
.demo-note { margin-left: auto; color: #9fb3c8; font-size: 12px; white-space: nowrap; }
@media (max-width: 640px) { .brand-sub { display: none; } }
</style>
