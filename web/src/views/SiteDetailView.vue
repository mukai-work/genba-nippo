<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, ApiError } from '@/api/client'
import type { DailyReportSummary, Site, WorkItem } from '@/api/types'
import NoticeBar from '@/components/NoticeBar.vue'
import ProgressBoard from '@/components/ProgressBoard.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDate, formatDateShort, formatMonth, shiftMonth, toIsoDate } from '@/domain/dates'
import { reportStatusLabel, reportStatusTone, weatherLabel } from '@/domain/labels'

const props = defineProps<{ siteId: number }>()
const route = useRoute()
const router = useRouter()

const today = toIsoDate(new Date())
// タブと表示月は URL に持たせる（日報を開いて戻ったときに同じ画面に戻れるように）
const tab = computed(() => (route.query.tab === 'reports' ? 'reports' : 'progress'))
const month = computed(() => (typeof route.query.month === 'string' ? route.query.month : today.slice(0, 7)))

const site = ref<Site | null>(null)
const workItems = ref<WorkItem[]>([])
const reports = ref<DailyReportSummary[]>([])
const loadingSite = ref(true)
const loadingReports = ref(true)
const error = ref('')

const showItemForm = ref(false)
const itemForm = reactive({ name: '', plannedStart: '', plannedEnd: '' })
const itemErrors = ref<Record<string, string[]>>({})
const savingItem = ref(false)

const behindItems = computed(() => workItems.value.filter((w) => w.scheduleStatus === 'Behind' || w.scheduleStatus === 'Overdue'))
const todaysReport = computed(() => reports.value.find((r) => r.reportDate === today))

function handle(e: unknown) {
  error.value = e instanceof ApiError ? e.message : String(e)
}

async function loadSite() {
  try {
    ;[site.value, workItems.value] = await Promise.all([api.getSite(props.siteId), api.listWorkItems(props.siteId)])
  } catch (e) {
    handle(e)
  } finally {
    loadingSite.value = false
  }
}

async function loadReports() {
  loadingReports.value = true
  try {
    reports.value = await api.listReports(props.siteId, month.value)
  } catch (e) {
    handle(e)
  } finally {
    loadingReports.value = false
  }
}

function setQuery(patch: Record<string, string>) {
  router.replace({ query: { ...route.query, ...patch } })
}

async function addWorkItem() {
  itemErrors.value = {}
  error.value = ''
  savingItem.value = true
  try {
    await api.createWorkItem(props.siteId, {
      name: itemForm.name,
      plannedStart: itemForm.plannedStart || null,
      plannedEnd: itemForm.plannedEnd || null,
    })
    Object.assign(itemForm, { name: '', plannedStart: '', plannedEnd: '' })
    showItemForm.value = false
    await loadSite()
  } catch (e) {
    handle(e)
    if (e instanceof ApiError) itemErrors.value = e.fieldErrors
  } finally {
    savingItem.value = false
  }
}

function openReport(r: DailyReportSummary) {
  router.push({ name: 'report', params: { siteId: props.siteId, reportId: r.id } })
}

function writeTodaysReport() {
  if (todaysReport.value) {
    openReport(todaysReport.value)
  } else {
    router.push({ name: 'report-new', params: { siteId: props.siteId }, query: { date: today } })
  }
}

onMounted(() => Promise.all([loadSite(), loadReports()]))
watch(month, loadReports)
</script>

<template>
  <main class="page">
    <div class="page-head">
      <div>
        <div class="breadcrumb"><RouterLink to="/">現場一覧</RouterLink></div>
        <h1 v-if="site">{{ site.name }}</h1>
        <h1 v-else><span class="skeleton" /></h1>
        <p v-if="site" class="sub">工期 {{ formatDate(site.startDate) }} 〜 {{ formatDate(site.endDate) }}</p>
      </div>
      <div v-if="site" class="head-stats">
        <div class="stat"><span class="sub">承認待ち</span><strong>{{ site.pendingApprovalCount }} 件</strong></div>
        <div class="stat">
          <span class="sub">遅れ・期限超過の工程</span>
          <strong :class="{ warn: behindItems.length > 0 }">{{ behindItems.length }} 工程</strong>
        </div>
        <button class="btn btn-primary" type="button" @click="writeTodaysReport">
          {{ todaysReport ? '今日の日報を開く' : '今日の日報を書く' }}
        </button>
      </div>
    </div>

    <NoticeBar v-if="error" :message="error" />

    <nav class="tabs">
      <button type="button" :class="{ active: tab === 'progress' }" @click="setQuery({ tab: 'progress' })">工程の進捗</button>
      <button type="button" :class="{ active: tab === 'reports' }" @click="setQuery({ tab: 'reports' })">作業日報</button>
    </nav>

    <template v-if="tab === 'progress'">
      <section class="panel">
        <div class="panel-head">
          <h2>工程表（予定 vs 実績）</h2>
          <button class="btn btn-sm" type="button" @click="showItemForm = !showItemForm">
            {{ showItemForm ? '閉じる' : '工程を追加' }}
          </button>
        </div>
        <form v-if="showItemForm" class="panel-body" style="border-bottom: 1px solid var(--border)" @submit.prevent="addWorkItem">
          <div class="form-grid">
            <div class="field" :class="{ 'has-error': itemErrors.name }">
              <label for="item-name">工程名</label>
              <input id="item-name" v-model="itemForm.name" maxlength="50" placeholder="例: 躯体工事" />
              <span v-for="m in itemErrors.name" :key="m" class="field-error">{{ m }}</span>
            </div>
            <div class="field" :class="{ 'has-error': itemErrors.plannedStart }">
              <label for="item-start">予定開始日</label>
              <input id="item-start" v-model="itemForm.plannedStart" type="date" :min="site?.startDate" :max="site?.endDate" />
              <span v-for="m in itemErrors.plannedStart" :key="m" class="field-error">{{ m }}</span>
            </div>
            <div class="field" :class="{ 'has-error': itemErrors.plannedEnd }">
              <label for="item-end">予定終了日</label>
              <input id="item-end" v-model="itemForm.plannedEnd" type="date" :min="site?.startDate" :max="site?.endDate" />
              <span v-for="m in itemErrors.plannedEnd" :key="m" class="field-error">{{ m }}</span>
            </div>
          </div>
          <div class="btn-row" style="margin-top: 12px">
            <button class="btn btn-primary btn-sm" type="submit" :disabled="savingItem">追加する</button>
          </div>
        </form>
        <div v-if="loadingSite" class="empty">読み込み中…</div>
        <div v-else-if="site && workItems.length > 0" class="panel-body">
          <ProgressBoard :work-items="workItems" :range-start="site.startDate" :range-end="site.endDate" :today="today" />
        </div>
        <div v-else class="empty">工程がまだありません。「工程を追加」から登録してください。</div>
      </section>
    </template>

    <template v-else>
      <section class="panel">
        <div class="panel-head">
          <div class="btn-row" style="align-items: center">
            <button class="btn btn-sm btn-month" type="button" aria-label="前の月" @click="setQuery({ month: shiftMonth(month, -1) })">‹</button>
            <h2 style="min-width: 110px; text-align: center">{{ formatMonth(month) }}</h2>
            <button class="btn btn-sm btn-month" type="button" aria-label="次の月" @click="setQuery({ month: shiftMonth(month, 1) })">›</button>
          </div>
          <a class="btn btn-sm" :href="api.exportUrl(siteId, month)" download>CSV（Excel用）をダウンロード</a>
        </div>
        <div v-if="loadingReports" class="empty">読み込み中…</div>
        <div v-else-if="reports.length === 0" class="empty">この月の日報はありません。</div>
        <div v-else class="table-wrap">
          <table class="stack-sm">
            <thead>
              <tr>
                <th>日付</th>
                <th>状態</th>
                <th>天候</th>
                <th class="num">作業員</th>
                <th>作業内容</th>
                <th aria-hidden="true" />
              </tr>
            </thead>
            <tbody>
              <tr v-for="r in reports" :key="r.id" class="clickable" @click="openReport(r)">
                <td style="white-space: nowrap">
                  <RouterLink :to="{ name: 'report', params: { siteId, reportId: r.id } }" @click.stop>{{ formatDateShort(r.reportDate) }}</RouterLink>
                </td>
                <td data-label="状態"><StatusBadge :label="reportStatusLabel[r.status]" :tone="reportStatusTone[r.status]" /></td>
                <td data-label="天候">{{ weatherLabel[r.weather] }}</td>
                <td class="num" data-label="作業員">{{ r.workerCount }} 人</td>
                <td class="summary" data-label="作業内容">{{ r.workSummary }}</td>
                <td class="chevron" aria-hidden="true">›</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </main>
</template>

<style scoped>
.summary { max-width: 520px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.warn { color: var(--tone-warning-fg); }
.head-stats { display: flex; align-items: center; flex-wrap: wrap; gap: 8px 24px; }
@media (max-width: 640px) { .summary { max-width: none; white-space: normal; } }
</style>
