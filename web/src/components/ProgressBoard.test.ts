import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ProgressBoard from './ProgressBoard.vue'
import type { WorkItem } from '@/api/types'

const base: Omit<WorkItem, 'id' | 'name' | 'scheduleStatus' | 'plannedPercent' | 'progressPercent'> = {
  siteId: 1,
  plannedStart: '2026-10-01',
  plannedEnd: '2026-10-10',
  lastReportedOn: null,
}

const items: WorkItem[] = [
  { ...base, id: 1, name: '基礎工事', scheduleStatus: 'Behind', plannedPercent: 50, progressPercent: 20 },
  { ...base, id: 2, name: '躯体工事', scheduleStatus: 'Completed', plannedPercent: 100, progressPercent: 100 },
]

function render() {
  return mount(ProgressBoard, {
    props: { workItems: items, rangeStart: '2026-10-01', rangeEnd: '2026-10-31', today: '2026-10-05' },
  })
}

describe('ProgressBoard', () => {
  it('工程ごとに状態のラベルと予定・実績を表示する', () => {
    const row = render().get('[data-testid="work-item-1"]')

    expect(row.text()).toContain('基礎工事')
    expect(row.text()).toContain('遅れ')
    expect(row.text()).toContain('50%')
    expect(row.text()).toContain('20%')
    expect(row.text()).toContain('−30pt') // 予定との差（単位つき）
  })

  it('完了した工程には差を出さない', () => {
    const row = render().get('[data-testid="work-item-2"]')
    expect(row.text()).toContain('完了')
    expect(row.find('.gap').exists()).toBe(false)
  })

  it('予定期間のバーの中を実績の割合だけ塗り、今日時点の予定の位置に線を引く', () => {
    const bar = render().get('[data-testid="work-item-1"] .bar')
    expect(bar.classes()).toContain('bar-Behind')
    expect(bar.attributes('style')).toContain('width: 32.25') // 10日 / 31日
    expect(bar.get('.bar-fill').attributes('style')).toContain('width: 20%')
    expect(bar.get('.plan-mark').attributes('style')).toContain('left: 50%')
  })

  it('今日の位置に縦線を引く', () => {
    expect(render().findAll('.today')).toHaveLength(items.length)
  })
})
