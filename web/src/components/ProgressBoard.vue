<script setup lang="ts">
import { computed } from 'vue'
import type { WorkItem } from '@/api/types'
import StatusBadge from './StatusBadge.vue'
import { formatDate } from '@/domain/dates'
import { scheduleStatusLabel, scheduleStatusTone } from '@/domain/labels'
import { barGeometry, markerPct, monthTicks } from '@/domain/timeline'

const props = defineProps<{
  workItems: WorkItem[]
  rangeStart: string
  rangeEnd: string
  today: string
}>()

const ticks = computed(() => monthTicks(props.rangeStart, props.rangeEnd))
const todayPct = computed(() => markerPct(props.rangeStart, props.rangeEnd, props.today))

const rows = computed(() =>
  props.workItems.map((item) => ({
    item,
    bar: barGeometry(props.rangeStart, props.rangeEnd, item.plannedStart, item.plannedEnd),
    gap: item.progressPercent - item.plannedPercent,
  })),
)
</script>

<template>
  <p class="scroll-hint sub">表は横にスクロールできます。</p>
  <div class="table-wrap">
    <table class="board">
      <thead>
        <tr>
          <th class="name-col">工程</th>
          <th class="period">予定期間</th>
          <th>状態</th>
          <th class="num">予定進捗</th>
          <th class="num">実績進捗</th>
          <th class="num">予定との差</th>
          <th class="timeline-head">
            <div class="timeline">
              <span v-for="t in ticks" :key="t.label + t.leftPct" class="tick" :style="{ left: `${t.leftPct}%` }">
                {{ t.showLabel ? t.label : '' }}
              </span>
            </div>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="{ item, bar, gap } in rows" :key="item.id" :data-testid="`work-item-${item.id}`">
          <td class="name name-col">{{ item.name }}</td>
          <td class="sub period">{{ formatDate(item.plannedStart) }} 〜 {{ formatDate(item.plannedEnd) }}</td>
          <td>
            <StatusBadge :label="scheduleStatusLabel[item.scheduleStatus]" :tone="scheduleStatusTone[item.scheduleStatus]" />
          </td>
          <td class="num">{{ item.plannedPercent }}%</td>
          <td class="num">{{ item.progressPercent }}%</td>
          <td class="num">
            <span v-if="gap < 0 && item.scheduleStatus !== 'Completed'" class="gap">−{{ -gap }}pt</span>
            <span v-else class="sub">—</span>
          </td>
          <td class="timeline-cell">
            <div class="timeline">
              <span v-for="t in ticks" :key="t.label + t.leftPct" class="grid" :style="{ left: `${t.leftPct}%` }" />
              <div
                class="bar"
                :class="`bar-${item.scheduleStatus}`"
                :style="{ left: `${bar.leftPct}%`, width: `${bar.widthPct}%` }"
                :title="`予定 ${item.plannedPercent}% / 実績 ${item.progressPercent}%`"
              >
                <div class="bar-fill" :style="{ width: `${item.progressPercent}%` }" />
                <span
                  v-if="item.plannedPercent > 0 && item.plannedPercent < 100"
                  class="plan-mark"
                  :style="{ left: `${item.plannedPercent}%` }"
                />
              </div>
              <span v-if="todayPct !== null" class="today" :style="{ left: `${todayPct}%` }" />
            </div>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
  <p class="legend sub">
      <span class="legend-item"><span class="legend-bar" />予定期間</span>
      <span class="legend-item"><span class="legend-fill" />実績（承認済みの日報から反映）</span>
      <span class="legend-item"><span class="legend-plan" />今日時点の予定</span>
      <span class="legend-item"><span class="legend-today" />今日</span>
      <span class="legend-item"><span class="legend-bar legend-behind" />遅れ</span>
      <span class="legend-item"><span class="legend-bar legend-overdue" />期限超過</span>
    </p>

</template>

<style scoped>
.board { min-width: 860px; }
.name { font-weight: 600; white-space: nowrap; }
.period { white-space: nowrap; }
.gap { color: var(--tone-danger-fg); }
.timeline-head, .timeline-cell { width: 42%; min-width: 320px; padding-top: 0; padding-bottom: 0; }
.timeline { position: relative; height: 36px; }
.tick { position: absolute; top: 10px; padding-left: 4px; border-left: 1px solid var(--border-strong); font-weight: 600; }
.grid { position: absolute; top: 0; bottom: 0; border-left: 1px dashed var(--border); }
/* 実績の塗りは常に濃紺。状態（遅れ・期限超過）は枠と地の色で示し、凡例と一致させる */
.bar {
  position: absolute; top: 11px; height: 14px; border-radius: 2px;
  background: var(--tone-neutral-bg); border: 1px solid var(--border-strong);
}
.bar-fill { height: 100%; background: var(--navy-soft); }
/* 「今日」の青い線（全高）と区別するため、予定の線はバーの中だけ・太め・濃色 */
.plan-mark { position: absolute; top: 1px; bottom: 1px; border-left: 3px solid var(--text); }
.bar-Behind { border-color: #d99a2b; background: var(--tone-warning-bg); }
.bar-Overdue { border-color: #d9776c; background: var(--tone-danger-bg); }
.today { position: absolute; top: 0; bottom: 0; border-left: 2px solid var(--accent); }
.legend { margin: 10px 0 0; display: flex; align-items: center; gap: 6px 16px; flex-wrap: wrap; }
.legend-item { display: inline-flex; align-items: center; gap: 6px; }
.legend-bar, .legend-fill, .legend-today, .legend-plan { display: inline-block; width: 18px; height: 10px; }
.legend-bar { border: 1px solid var(--border-strong); background: var(--tone-neutral-bg); }
.legend-behind { border-color: #d99a2b; background: var(--tone-warning-bg); }
.legend-overdue { border-color: #d9776c; background: var(--tone-danger-bg); }
.legend-fill { background: var(--navy-soft); }
.legend-plan { width: 2px; height: 14px; background: var(--text); }
.legend-today { width: 2px; height: 14px; background: var(--accent); }
.scroll-hint { display: none; margin: 0 0 6px; }
@media (max-width: 640px) {
  .scroll-hint { display: block; }
  .period { display: none; }
  /* 横にスクロールしても工程名が見えるように固定する */
  .name-col { position: sticky; left: 0; z-index: 1; background: var(--surface); }
  th.name-col { background: var(--surface-sub); }
}
</style>
