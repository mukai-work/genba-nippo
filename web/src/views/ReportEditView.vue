<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, ApiError } from '@/api/client'
import type { DailyReport, ReportContent, Site, WorkItem } from '@/api/types'
import NoticeBar from '@/components/NoticeBar.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDate, formatDateShort, formatDateTime, toIsoDate } from '@/domain/dates'
import { reportStatusLabel, reportStatusTone, weatherLabel, weatherOptions } from '@/domain/labels'
import { availableActions, isEditable, type ReportAction } from '@/domain/reportActions'

const props = defineProps<{ siteId: number; reportId: number | null; initialDate?: string | null }>()
const router = useRouter()

const site = ref<Site | null>(null)
const workItems = ref<WorkItem[]>([])
const report = ref<DailyReport | null>(null)
const loading = ref(true)

const form = reactive({
  reportDate: props.initialDate ?? toIsoDate(new Date()),
  weather: 'Sunny' as ReportContent['weather'],
  workerCount: 0 as number | '',
  workSummary: '',
  /** 工程ID → 入力中の進捗率（空欄は「この日は報告しない」） */
  progress: {} as Record<number, number | ''>,
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
const canSubmit = computed(() => form.workSummary.trim() !== '')

/** 入力欄を出す工程。完了済みでこの日報に記載のない工程は出さない（空欄が並ぶのを避ける） */
const inputItems = computed(() =>
  workItems.value.filter((w) => w.scheduleStatus !== 'Completed' || form.progress[w.id] !== undefined),
)
const completedItems = computed(() => workItems.value.filter((w) => !inputItems.value.includes(w)))

/** 閲覧表示用：報告された工程だけを、現在の実績と並べる */
const reportedRows = computed(() =>
  (report.value?.progressEntries ?? []).map((e) => {
    const current = workItems.value.find((w) => w.id === e.workItemId)?.progressPercent ?? null
    return { ...e, current, diff: current === null ? null : e.progressPercent - current }
  }),
)

function fill(r: DailyReport) {
  report.value = r
  form.reportDate = r.reportDate
  form.weather = r.weather
  form.workerCount = r.workerCount
  form.workSummary = r.workSummary
  form.progress = Object.fromEntries(r.progressEntries.map((e) => [e.workItemId, e.progressPercent]))
}

async function load() {
  loading.value = true
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
  } finally {
    loading.value = false
  }
}

/** 画面側の入力チェック。サーバーでも同じチェックをするが、送る前に分かるものはここで止める */
function validate(): boolean {
  const errors: Record<string, string[]> = {}
  if (report.value === null && !form.reportDate) errors.reportDate = ['日付を入力してください。']
  if (form.workerCount === '' || !Number.isInteger(form.workerCount) || form.workerCount < 0 || form.workerCount > 999) {
    errors.workerCount = ['作業員数は0〜999人で入力してください。']
  }
  for (const [id, v] of Object.entries(form.progress)) {
    if (v !== '' && (!Number.isInteger(v) || v < 0 || v > 100)) errors[`progress-${id}`] = ['0〜100で入力してください。']
  }
  fieldErrors.value = errors
  if (Object.keys(errors).length > 0) {
    error.value = '入力内容を確認してください。'
    return false
  }
  return true
}

function content(): ReportContent {
  return {
    weather: form.weather,
    workerCount: Number(form.workerCount),
    workSummary: form.workSummary,
    progressEntries: Object.entries(form.progress)
      .filter(([, v]) => v !== '')
      .map(([id, v]) => ({ workItemId: Number(id), progressPercent: Number(v) })),
  }
}

function showError(e: unknown) {
  if (e instanceof ApiError) {
    error.value = e.message
    conflict.value = e.isConcurrencyConflict
    fieldErrors.value = e.fieldErrors
  } else {
    error.value = String(e)
  }
}

/** 入力内容を保存する（新規なら作成）。保存後の最新の日報を画面に反映して返す */
async function save(): Promise<DailyReport> {
  if (report.value === null) {
    const created = await api.createReport(props.siteId, { ...content(), reportDate: form.reportDate })
    // 続く処理（提出）が失敗しても「作成済みの日報」として扱えるよう、URL より先に画面の状態を更新する
    fill(created)
    await router.replace({ name: 'report', params: { siteId: props.siteId, reportId: created.id } })
    return created
  }
  const updated = await api.updateReport(report.value.id, report.value.version, content())
  fill(updated)
  return updated
}

const doneMessage: Record<ReportAction, string> = {
  save: '保存しました。',
  submit: '提出しました。承認されると工程の進捗に反映されます。',
  approve: '承認しました。工程の進捗に反映しました。',
  reject: '差し戻しました。',
}

async function run(action: ReportAction) {
  error.value = ''
  done.value = ''
  conflict.value = false
  fieldErrors.value = {}
  if ((action === 'save' || action === 'submit') && !validate()) {
    window.scrollTo({ top: 0, behavior: 'smooth' })
    return
  }

  busy.value = true
  try {
    switch (action) {
      case 'save':
        await save()
        break
      case 'submit': {
        const saved = await save() // 入力中の内容を保存してから提出する
        fill(await api.submitReport(saved.id, saved.version))
        break
      }
      case 'approve':
        fill(await api.approveReport(report.value!.id, report.value!.version))
        workItems.value = await api.listWorkItems(props.siteId) // 反映後の実績を表示し直す
        break
      case 'reject':
        fill(await api.rejectReport(report.value!.id, report.value!.version, rejectReason.value))
        showReject.value = false
        rejectReason.value = ''
        break
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
        </div>
        <h1 v-if="loading"><span class="skeleton" /></h1>
        <h1 v-else>
          作業日報 {{ report ? formatDateShort(report.reportDate) : '（新規）' }}
          <StatusBadge v-if="status" :label="reportStatusLabel[status]" :tone="reportStatusTone[status]" />
        </h1>
        <p v-if="report?.submittedAt" class="sub">
          提出 {{ formatDateTime(report.submittedAt) }}
          <template v-if="report.approvedAt"> ／ 承認 {{ formatDateTime(report.approvedAt) }}</template>
        </p>
      </div>
    </div>

    <NoticeBar v-if="error" :message="error">
      <button v-if="conflict" class="btn btn-sm" type="button" @click="load">最新の内容を読み込む</button>
    </NoticeBar>
    <NoticeBar v-if="done" :message="done" tone="success" />
    <NoticeBar
      v-if="report?.status === 'Rejected' && report.rejectionReason"
      tone="warning"
      :message="`差戻しの理由：${report.rejectionReason}`"
    />

    <div v-if="loading" class="panel empty">読み込み中…</div>

    <!-- 承認待ち・承認済み：読むための表示 -->
    <template v-else-if="!editable && report">
      <section class="panel">
        <div class="panel-head"><h2>作業の記録</h2></div>
        <dl class="panel-body readout">
          <div><dt>日付</dt><dd>{{ formatDate(report.reportDate) }}</dd></div>
          <div><dt>天候</dt><dd>{{ weatherLabel[report.weather] }}</dd></div>
          <div><dt>作業員数</dt><dd>{{ report.workerCount }} 人</dd></div>
          <div class="wide"><dt>作業内容</dt><dd>{{ report.workSummary }}</dd></div>
        </dl>
      </section>

      <section class="panel">
        <div class="panel-head">
          <h2>報告された進捗</h2>
          <span v-if="status === 'Submitted'" class="sub">承認すると、この値が工程の実績になります。</span>
        </div>
        <div v-if="reportedRows.length === 0" class="empty">この日報には進捗の報告がありません。</div>
        <div v-else class="table-wrap">
          <table class="stack-sm progress-readout">
            <thead>
              <tr>
                <th>工程</th>
                <th v-if="status === 'Submitted'" class="num">現在の実績</th>
                <th class="num">報告</th>
                <th v-if="status === 'Submitted'" class="num">差</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in reportedRows" :key="row.workItemId">
                <td><strong>{{ row.workItemName }}</strong></td>
                <td v-if="status === 'Submitted'" class="num" data-label="現在の実績">{{ row.current === null ? '—' : `${row.current}%` }}</td>
                <td class="num" data-label="報告">{{ row.progressPercent }}%</td>
                <td v-if="status === 'Submitted'" class="num" data-label="差">
                  <template v-if="row.diff !== null">{{ row.diff >= 0 ? '+' : '−' }}{{ Math.abs(row.diff) }}pt</template>
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
        </div>
      </section>
    </template>

    <!-- 新規・下書き・差戻し：入力フォーム -->
    <template v-else>
      <section class="panel">
        <div class="panel-head"><h2>作業の記録</h2></div>
        <div class="panel-body">
          <div class="form-grid">
            <div v-if="report" class="field">
              <span class="label">日付</span>
              <span class="fixed-value">{{ formatDateShort(report.reportDate) }}</span>
            </div>
            <div v-else class="field" :class="{ 'has-error': fieldErrors.reportDate }">
              <label for="report-date">日付</label>
              <input id="report-date" v-model="form.reportDate" type="date" required :min="site?.startDate" :max="site?.endDate" />
              <span v-for="m in fieldErrors.reportDate" :key="m" class="field-error">{{ m }}</span>
            </div>
            <div class="field">
              <span class="label">天候</span>
              <div class="segmented" role="radiogroup" aria-label="天候">
                <label v-for="o in weatherOptions" :key="o.value" :class="{ checked: form.weather === o.value }">
                  <input v-model="form.weather" type="radio" name="weather" :value="o.value" />{{ o.label }}
                </label>
              </div>
            </div>
            <div class="field" :class="{ 'has-error': fieldErrors.workerCount }">
              <label for="worker-count">作業員数（人）</label>
              <input id="worker-count" v-model.number="form.workerCount" type="number" min="0" max="999" required />
              <span v-for="m in fieldErrors.workerCount" :key="m" class="field-error">{{ m }}</span>
            </div>
            <div class="field wide" :class="{ 'has-error': fieldErrors.workSummary }">
              <label for="work-summary">作業内容</label>
              <textarea
                id="work-summary" v-model="form.workSummary" maxlength="2000"
                placeholder="例: 躯体1階柱の配筋。午後から型枠建込み。"
              />
              <span v-for="m in fieldErrors.workSummary" :key="m" class="field-error">{{ m }}</span>
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
          <table class="stack-sm">
            <thead>
              <tr>
                <th>工程</th>
                <th>予定期間</th>
                <th class="num">現在の実績</th>
                <th style="width: 160px">この日の進捗率</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="w in inputItems" :key="w.id" :class="{ muted: !inPeriod(w) }">
                <td>
                  <strong>{{ w.name }}</strong>
                  <StatusBadge v-if="inPeriod(w)" label="予定期間中" tone="info" class="period-badge" />
                </td>
                <td class="sub hide-sm">{{ formatDate(w.plannedStart) }} 〜 {{ formatDate(w.plannedEnd) }}</td>
                <td class="num" data-label="現在の実績">{{ w.progressPercent }}%</td>
                <td data-label="この日の進捗率" :class="{ 'has-error': fieldErrors[`progress-${w.id}`] }">
                  <span class="percent-input">
                    <input v-model.number="form.progress[w.id]" type="number" min="0" max="100" :aria-label="`${w.name}の進捗率`" />
                    <span>%</span>
                  </span>
                  <span v-for="m in fieldErrors[`progress-${w.id}`]" :key="m" class="field-error">{{ m }}</span>
                </td>
              </tr>
            </tbody>
          </table>
          <p v-if="completedItems.length > 0" class="sub completed-note">
            完了済みの工程（{{ completedItems.map((w) => w.name).join('、') }}）は入力欄を省いています。
          </p>
        </div>
      </section>
    </template>

    <div v-if="!loading && actions.length > 0" class="action-bar">
      <template v-if="actions.includes('reject')">
        <button v-if="!showReject" class="btn btn-danger" type="button" :disabled="busy" @click="showReject = true">差し戻す…</button>
        <template v-else>
          <button class="btn btn-danger" type="button" :disabled="busy || rejectReason.trim() === ''" @click="run('reject')">
            差戻しを確定
          </button>
          <button class="btn" type="button" @click="showReject = false">やめる</button>
        </template>
      </template>
      <div class="push-right btn-row">
        <button v-if="actions.includes('save')" class="btn" type="button" :disabled="busy" @click="run('save')">下書き保存</button>
        <button
          v-if="actions.includes('submit')" class="btn btn-primary" type="button" :disabled="busy || !canSubmit"
          :title="canSubmit ? '' : '作業内容を入力すると提出できます'" @click="run('submit')"
        >提出する</button>
        <button v-if="actions.includes('approve')" class="btn btn-primary" type="button" :disabled="busy || showReject" @click="run('approve')">
          承認する
        </button>
      </div>
    </div>
    <p v-else-if="status === 'Approved'" class="sub" style="margin-top: 16px">承認済みの日報は編集できません。</p>
  </main>
</template>

<style scoped>
.percent-input { display: inline-flex; align-items: center; gap: 6px; }
.percent-input input { width: 96px; text-align: right; }
.period-badge { margin-left: 6px; }
.fixed-value { display: flex; align-items: center; height: 36px; }
/* 数字の列が工程名から離れすぎないよう、表の幅を絞る */
.progress-readout { max-width: 640px; }
.completed-note { margin: 0; padding: 10px 12px; border-top: 1px solid var(--border); }
tr.muted td { color: var(--text-sub); }
</style>
