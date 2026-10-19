<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, ApiError } from '@/api/client'
import type { DailyReport, ReportContent, Site, WorkItem } from '@/api/types'
import NoticeBar from '@/components/NoticeBar.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDate, formatDateShort, toIsoDate } from '@/domain/dates'
import { reportStatusLabel, reportStatusTone, weatherOptions } from '@/domain/labels'
import { availableActions, isEditable, type ReportAction } from '@/domain/reportActions'

const props = defineProps<{ siteId: number; reportId: number | null; initialDate?: string | null }>()
const router = useRouter()

const site = ref<Site | null>(null)
const workItems = ref<WorkItem[]>([])
const report = ref<DailyReport | null>(null)

const form = reactive({
  reportDate: props.initialDate ?? toIsoDate(new Date()),
  weather: 'Sunny' as ReportContent['weather'],
  workerCount: 0,
  workSummary: '',
  /** 工程ID → 入力中の進捗率（空欄は「今日は報告しない」） */
  progress: {} as Record<number, string>,
})
const rejectReason = ref('')
const showReject = ref(false)

const busy = ref(false)
const error = ref('')
const conflict = ref(false)
const done = ref('')
const fieldErrors = ref<Record<string, string[]>>({})

const status = computed(() => report.value?.status ?? null)
const actions = computed(() => availableActions(status.value))
const editable = computed(() => isEditable(status.value))

function fill(r: DailyReport) {
  report.value = r
  form.reportDate = r.reportDate
  form.weather = r.weather
  form.workerCount = r.workerCount
  form.workSummary = r.workSummary
  form.progress = Object.fromEntries(r.progressEntries.map((e) => [e.workItemId, String(e.progressPercent)]))
}

async function load() {
  error.value = ''
  conflict.value = false
  try {
    const [s, items, r] = await Promise.all([
      api.getSite(props.siteId),
      api.listWorkItems(props.siteId),
      props.reportId === null ? Promise.resolve(null) : api.getReport(props.reportId),
    ])
    site.value = s
    workItems.value = items
    if (r) fill(r)
  } catch (e) {
    showError(e)
  }
}

function content(): ReportContent {
  return {
    weather: form.weather,
    workerCount: Number(form.workerCount),
    workSummary: form.workSummary,
    progressEntries: Object.entries(form.progress)
      .filter(([, v]) => v !== '' && v !== null && v !== undefined)
      .map(([id, v]) => ({ workItemId: Number(id), progressPercent: Number(v) })),
  }
}

function showError(e: unknown) {
  if (e instanceof ApiError) {
    error.value = e.message
    conflict.value = e.isConflict && e.message.includes('再読み込み')
    fieldErrors.value = e.fieldErrors
  } else {
    error.value = String(e)
  }
}

/** 入力内容を保存する（新規なら作成）。保存後の最新の日報を返す */
async function save(): Promise<DailyReport> {
  if (report.value === null) {
    const created = await api.createReport(props.siteId, { ...content(), reportDate: form.reportDate })
    await router.replace({ name: 'report', params: { siteId: props.siteId, reportId: created.id } })
    return created
  }
  return api.updateReport(report.value.id, report.value.version, content())
}

const doneMessage: Record<ReportAction, string> = {
  save: '保存しました。',
  submit: '提出しました。承認されると工程の進捗に反映されます。',
  approve: '承認しました。工程の進捗に反映しました。',
  reject: '差し戻しました。',
}

async function run(action: ReportAction) {
  busy.value = true
  error.value = ''
  done.value = ''
  fieldErrors.value = {}
  try {
    let r: DailyReport
    switch (action) {
      case 'save':
        r = await save()
        break
      case 'submit': {
        const saved = await save() // 入力中の内容を保存してから提出する
        r = await api.submitReport(saved.id, saved.version)
        break
      }
      case 'approve':
        r = await api.approveReport(report.value!.id, report.value!.version)
        break
      case 'reject':
        r = await api.rejectReport(report.value!.id, report.value!.version, rejectReason.value)
        showReject.value = false
        rejectReason.value = ''
        break
    }
    fill(r)
    if (action === 'approve') {
      workItems.value = await api.listWorkItems(props.siteId) // 反映後の実績を表示し直す
    }
    done.value = doneMessage[action]
  } catch (e) {
    showError(e)
  } finally {
    busy.value = false
    // ボタンは画面の下、結果のお知らせは上にあるので、見える位置まで戻す
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }
}

function inPeriod(w: WorkItem) {
  return w.plannedStart <= form.reportDate && form.reportDate <= w.plannedEnd
}

onMounted(load)
</script>

<template>
  <main class="page">
    <div class="page-head">
      <div>
        <div class="breadcrumb">
          <RouterLink to="/">現場一覧</RouterLink> ／
          <RouterLink :to="{ name: 'site', params: { siteId }, query: { tab: 'reports', month: form.reportDate.slice(0, 7) } }">
            {{ site?.name ?? '現場' }}
          </RouterLink>
          ／ 作業日報
        </div>
        <h1>
          作業日報 {{ report ? formatDateShort(report.reportDate) : '（新規）' }}
          <StatusBadge v-if="status" :label="reportStatusLabel[status]" :tone="reportStatusTone[status]" />
        </h1>
        <p v-if="report?.submittedAt" class="sub">
          提出 {{ new Date(report.submittedAt).toLocaleString('ja-JP') }}
          <template v-if="report.approvedAt"> ／ 承認 {{ new Date(report.approvedAt).toLocaleString('ja-JP') }}</template>
        </p>
      </div>
    </div>

    <NoticeBar v-if="error" :message="error">
      <button v-if="conflict" class="btn btn-sm" type="button" @click="load">最新の内容を読み込む</button>
    </NoticeBar>
    <NoticeBar v-if="done" :message="done" tone="success" />
    <NoticeBar v-if="report?.status === 'Rejected' && report.rejectionReason" tone="warning" :message="`差戻しの理由: ${report.rejectionReason}`" />

    <section class="panel">
      <div class="panel-head"><h2>作業の記録</h2></div>
      <div class="panel-body">
        <div class="form-grid">
          <div class="field" :class="{ 'has-error': fieldErrors.reportDate }">
            <label for="report-date">日付</label>
            <input
              id="report-date" v-model="form.reportDate" type="date" :disabled="report !== null"
              :min="site?.startDate" :max="site?.endDate"
            />
          </div>
          <div class="field">
            <span class="label">天候</span>
            <div class="segmented" role="radiogroup" aria-label="天候">
              <label v-for="o in weatherOptions" :key="o.value" :class="{ checked: form.weather === o.value }">
                <input v-model="form.weather" type="radio" name="weather" :value="o.value" :disabled="!editable" />{{ o.label }}
              </label>
            </div>
          </div>
          <div class="field" :class="{ 'has-error': fieldErrors.workerCount }">
            <label for="worker-count">作業員数（人）</label>
            <input id="worker-count" v-model.number="form.workerCount" type="number" min="0" max="999" :disabled="!editable" />
            <span v-for="m in fieldErrors.workerCount" :key="m" class="field-error">{{ m }}</span>
          </div>
          <div class="field wide" :class="{ 'has-error': fieldErrors.workSummary }">
            <label for="work-summary">作業内容</label>
            <textarea
              id="work-summary" v-model="form.workSummary" maxlength="2000" :disabled="!editable"
              placeholder="例: 躯体1階柱の配筋。午後から型枠建込み。"
            />
          </div>
        </div>
      </div>
    </section>

    <section class="panel">
      <div class="panel-head">
        <h2>工程ごとの進捗（この日の終わり時点）</h2>
        <span class="sub">報告しない工程は空欄のままにします。承認されると工程表に反映されます。</span>
      </div>
      <div v-if="workItems.length === 0" class="empty">この現場には工程がまだありません。</div>
      <div v-else class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>工程</th>
              <th>予定期間</th>
              <th class="num">現在の実績</th>
              <th style="width: 160px">この日の進捗率</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="w in workItems" :key="w.id" :class="{ muted: !inPeriod(w) }">
              <td>
                <strong>{{ w.name }}</strong>
                <span v-if="inPeriod(w)" class="sub" style="margin-left: 6px">予定期間中</span>
              </td>
              <td class="sub">{{ formatDate(w.plannedStart) }} 〜 {{ formatDate(w.plannedEnd) }}</td>
              <td class="num">{{ w.progressPercent }}%</td>
              <td>
                <div class="percent-input">
                  <input
                    v-model="form.progress[w.id]" type="number" min="0" max="100" :disabled="!editable"
                    :aria-label="`${w.name}の進捗率`"
                  />
                  <span>%</span>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <section v-if="showReject" class="panel">
      <div class="panel-body">
        <div class="field">
          <label for="reject-reason">差戻しの理由</label>
          <textarea id="reject-reason" v-model="rejectReason" maxlength="500" placeholder="例: 作業員数が0人になっています。" />
        </div>
        <div class="btn-row" style="margin-top: 12px">
          <button class="btn btn-danger" type="button" :disabled="busy || rejectReason.trim() === ''" @click="run('reject')">差し戻す</button>
          <button class="btn" type="button" @click="showReject = false">やめる</button>
        </div>
      </div>
    </section>

    <div class="btn-row actions">
      <button v-if="actions.includes('save')" class="btn" type="button" :disabled="busy" @click="run('save')">下書き保存</button>
      <button v-if="actions.includes('submit')" class="btn btn-primary" type="button" :disabled="busy" @click="run('submit')">提出する</button>
      <button v-if="actions.includes('approve')" class="btn btn-primary" type="button" :disabled="busy" @click="run('approve')">承認する</button>
      <button v-if="actions.includes('reject') && !showReject" class="btn btn-danger" type="button" :disabled="busy" @click="showReject = true">
        差し戻す…
      </button>
      <span v-if="status === 'Approved'" class="sub">承認済みの日報は編集できません。</span>
    </div>
  </main>
</template>

<style scoped>
.actions { margin-top: 16px; align-items: center; }
.percent-input { display: flex; align-items: center; gap: 6px; }
.percent-input input { width: 96px; text-align: right; }
tr.muted td { color: var(--text-sub); }
</style>
