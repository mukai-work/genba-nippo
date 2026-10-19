import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { DailyReport, Site, WorkItem } from '@/api/types'

// API は差し替えて、画面の状態遷移だけを確かめる
const api = vi.hoisted(() => ({
  getSite: vi.fn(),
  listWorkItems: vi.fn(),
  getReport: vi.fn(),
  createReport: vi.fn(),
  updateReport: vi.fn(),
  submitReport: vi.fn(),
  approveReport: vi.fn(),
  rejectReport: vi.fn(),
}))
vi.mock('@/api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/client')>()),
  api,
}))

import { ApiError } from '@/api/client'
import ReportEditView from './ReportEditView.vue'

const site: Site = {
  id: 1, name: 'テスト現場', address: null, startDate: '2026-10-01', endDate: '2026-12-31',
  pendingApprovalCount: 0, delayedWorkItemCount: 0,
}
const items: WorkItem[] = [
  {
    id: 10, siteId: 1, name: '躯体工事', plannedStart: '2026-10-10', plannedEnd: '2026-10-29',
    progressPercent: 10, lastReportedOn: null, plannedPercent: 45, scheduleStatus: 'Behind',
  },
  {
    id: 11, siteId: 1, name: '仮設工事', plannedStart: '2026-10-01', plannedEnd: '2026-10-05',
    progressPercent: 100, lastReportedOn: '2026-10-05', plannedPercent: 100, scheduleStatus: 'Completed',
  },
]

function report(patch: Partial<DailyReport> = {}): DailyReport {
  return {
    id: 5, siteId: 1, reportDate: '2026-10-18', weather: 'Sunny', workerCount: 8, workSummary: '',
    status: 'Draft', rejectionReason: null, submittedAt: null, approvedAt: null, version: 1,
    progressEntries: [], ...patch,
  }
}

async function render(reportId: number | null) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div />' } },
      { path: '/sites/:siteId', name: 'site', component: { template: '<div />' } },
      { path: '/sites/:siteId/reports/:reportId', name: 'report', component: { template: '<div />' } },
    ],
  })
  await router.push('/')
  const wrapper = mount(ReportEditView, {
    props: { siteId: 1, reportId, initialDate: '2026-10-18' },
    global: { plugins: [router] },
  })
  await flushPromises()
  return wrapper
}

const button = (wrapper: Awaited<ReturnType<typeof render>>, text: string) =>
  wrapper.findAll('button').find((b) => b.text() === text)!

beforeEach(() => {
  vi.clearAllMocks()
  window.scrollTo = vi.fn()
  api.getSite.mockResolvedValue(site)
  api.listWorkItems.mockResolvedValue(items)
})

describe('ReportEditView', () => {
  it('新規作成は成功し提出だけ失敗しても、2回目の提出で日報を二重に作らない', async () => {
    api.createReport.mockResolvedValue(report({ workSummary: '配筋' }))
    api.submitReport
      .mockRejectedValueOnce(new ApiError(400, { detail: '作業内容を入力してから提出してください。' }))
      .mockResolvedValueOnce(report({ status: 'Submitted', version: 3, workSummary: '配筋' }))
    api.updateReport.mockResolvedValue(report({ version: 2, workSummary: '配筋' }))

    const wrapper = await render(null)
    await wrapper.get('#work-summary').setValue('配筋')

    await button(wrapper, '提出する').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('作業内容を入力してから提出してください。')

    await button(wrapper, '提出する').trigger('click')
    await flushPromises()

    expect(api.createReport).toHaveBeenCalledTimes(1) // 2回目は作成ではなく更新
    expect(api.updateReport).toHaveBeenCalledWith(5, 1, expect.anything())
    expect(api.submitReport).toHaveBeenLastCalledWith(5, 2)
  })

  it('作業内容が空のあいだは提出できない', async () => {
    const wrapper = await render(null)
    expect(button(wrapper, '提出する').attributes('disabled')).toBeDefined()

    await wrapper.get('#work-summary').setValue('型枠')
    expect(button(wrapper, '提出する').attributes('disabled')).toBeUndefined()
  })

  it('作業員数が空なら送信せずに入力欄でエラーを出す', async () => {
    const wrapper = await render(null)
    await wrapper.get('#worker-count').setValue('')

    await button(wrapper, '下書き保存').trigger('click')
    await flushPromises()

    expect(api.createReport).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('作業員数は0〜999人で入力してください。')
  })

  it('完了済みの工程には入力欄を出さない', async () => {
    const wrapper = await render(null)
    expect(wrapper.find('[aria-label="躯体工事の進捗率"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="仮設工事の進捗率"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('完了済みの工程（仮設工事）')
  })

  it('承認待ちの日報は入力欄ではなく読み取り表示にし、現在の実績からの変化を出す', async () => {
    api.getReport.mockResolvedValue(report({
      status: 'Submitted', version: 2, workSummary: '柱の配筋',
      progressEntries: [{ workItemId: 10, workItemName: '躯体工事', progressPercent: 25 }],
    }))

    const wrapper = await render(5)

    expect(wrapper.find('textarea#work-summary').exists()).toBe(false)
    expect(wrapper.text()).toContain('柱の配筋')
    const cells = wrapper.findAll('.progress-readout tbody td').map((td) => td.text())
    expect(cells).toEqual(['躯体工事', '10%', '25%', '+15pt']) // 工程・現在の実績・報告・差
    expect(button(wrapper, '承認する').exists()).toBe(true)
  })
})
