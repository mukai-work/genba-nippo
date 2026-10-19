<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, ApiError } from '@/api/client'
import type { Site } from '@/api/types'
import NoticeBar from '@/components/NoticeBar.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDate } from '@/domain/dates'

const router = useRouter()
const sites = ref<Site[]>([])
const loading = ref(true)
const error = ref('')

const showForm = ref(false)
const saving = ref(false)
const form = reactive({ name: '', address: '', startDate: '', endDate: '' })
const fieldErrors = ref<Record<string, string[]>>({})

async function load() {
  loading.value = true
  error.value = ''
  try {
    sites.value = await api.listSites()
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

async function createSite() {
  saving.value = true
  error.value = ''
  fieldErrors.value = {}
  try {
    const created = await api.createSite({ ...form, address: form.address || null })
    await router.push({ name: 'site', params: { siteId: created.id } })
  } catch (e) {
    if (e instanceof ApiError) {
      error.value = e.message
      fieldErrors.value = e.fieldErrors
    } else {
      error.value = String(e)
    }
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <main class="page">
    <div class="page-head">
      <div>
        <h1>現場一覧</h1>
        <p class="sub">承認待ちの日報と、遅れている工程の数を現場ごとに表示します。</p>
      </div>
      <button class="btn btn-primary" type="button" @click="showForm = !showForm">
        {{ showForm ? '閉じる' : '現場を登録' }}
      </button>
    </div>

    <NoticeBar v-if="error" :message="error" />

    <section v-if="showForm" class="panel">
      <div class="panel-head"><h2>現場を登録</h2></div>
      <form class="panel-body" @submit.prevent="createSite">
        <div class="form-grid">
          <div class="field wide" :class="{ 'has-error': fieldErrors.name }">
            <label for="site-name">現場名</label>
            <input id="site-name" v-model="form.name" maxlength="100" placeholder="例: みどり町 集合住宅新築工事" />
            <span v-for="m in fieldErrors.name" :key="m" class="field-error">{{ m }}</span>
          </div>
          <div class="field wide">
            <label for="site-address">所在地（任意）</label>
            <input id="site-address" v-model="form.address" maxlength="200" />
          </div>
          <div class="field" :class="{ 'has-error': fieldErrors.startDate }">
            <label for="site-start">工期（開始）</label>
            <input id="site-start" v-model="form.startDate" type="date" />
            <span v-for="m in fieldErrors.startDate" :key="m" class="field-error">{{ m }}</span>
          </div>
          <div class="field" :class="{ 'has-error': fieldErrors.endDate }">
            <label for="site-end">工期（終了）</label>
            <input id="site-end" v-model="form.endDate" type="date" />
            <span v-for="m in fieldErrors.endDate" :key="m" class="field-error">{{ m }}</span>
          </div>
        </div>
        <div class="btn-row" style="margin-top: 16px">
          <button class="btn btn-primary" type="submit" :disabled="saving">登録する</button>
        </div>
      </form>
    </section>

    <section class="panel">
      <div v-if="loading" class="empty">読み込み中…</div>
      <div v-else-if="sites.length === 0" class="empty">まだ現場がありません。「現場を登録」から追加してください。</div>
      <div v-else class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>現場名</th>
              <th>工期</th>
              <th>承認待ちの日報</th>
              <th>遅れている工程</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="site in sites"
              :key="site.id"
              class="clickable"
              @click="router.push({ name: 'site', params: { siteId: site.id } })"
            >
              <td>
                <RouterLink :to="{ name: 'site', params: { siteId: site.id } }" @click.stop>{{ site.name }}</RouterLink>
                <div v-if="site.address" class="sub">{{ site.address }}</div>
              </td>
              <td class="sub">{{ formatDate(site.startDate) }} 〜 {{ formatDate(site.endDate) }}</td>
              <td>
                <StatusBadge v-if="site.pendingApprovalCount > 0" :label="`${site.pendingApprovalCount} 件`" tone="info" />
                <span v-else class="sub">なし</span>
              </td>
              <td>
                <StatusBadge v-if="site.delayedWorkItemCount > 0" :label="`${site.delayedWorkItemCount} 工程`" tone="warning" />
                <span v-else class="sub">なし</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </main>
</template>
