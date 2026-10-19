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
  <div class="table-wrap">
    <table class="board">
      <thead>
        <tr>
          <th>工程</th>
          <th>予定期間</th>
          <th>状態</th>
          <th class="num">予定</th>
          <th class="num">実績</th>
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
          <td class="name">{{ item.name }}</td>
          <td class="sub period">{{ formatDate(item.plannedStart) }} 〜 {{ formatDate(item.plannedEnd) }}</td>
          <td>
            <StatusBadge :label="scheduleStatusLabel[item.scheduleStatus]" :tone="scheduleStatusTone[item.scheduleStatus]" />
          </td>
          <td class="num">{{ item.plannedPercent }}%</td>
          <td class="num">
            {{ item.progressPercent }}%
            <span v-if="gap < 0 && item.scheduleStatus !== 'Completed'" class="gap">{{ gap }}</span>
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
              </div>
              <span v-if="todayPct !== null" class="today" :style="{ left: `${todayPct}%` }" />
            </div>
          </td>
        </tr>
      </tbody>
    </table>
    <p class="legend sub">
      <span class="legend-bar" /> 予定期間　<span class="legend-fill" /> 実績（承認済みの日報から反映）　<span class="legend-today" /> 今日
    </p>
  </div>
</template>

<style scoped>
.board { min-width: 860px; }
.name { font-weight: 600; white-space: nowrap; }
.period { white-space: nowrap; }
.gap { margin-left: 4px; color: var(--tone-danger-fg); font-size: 12px; }
.timeline-head, .timeline-cell { width: 42%; min-width: 320px; padding-top: 0; padding-bottom: 0; }
.timeline { position: relative; height: 36px; }
.tick { position: absolute; top: 10px; padding-left: 4px; border-left: 1px solid var(--border-strong); font-weight: 600; }
.grid { position: absolute; top: 0; bottom: 0; border-left: 1px dashed var(--border); }
.bar {
  position: absolute; top: 11px; height: 14px; border-radius: 2px; overflow: hidden;
  background: var(--tone-neutral-bg); border: 1px solid var(--border-strong);
}
.bar-fill { height: 100%; background: var(--navy-soft); }
.bar-Behind { border-color: #e0a84a; background: var(--tone-warning-bg); }
.bar-Behind .bar-fill { background: #c77800; }
.bar-Overdue { border-color: #e39b93; background: var(--tone-danger-bg); }
.bar-Overdue .bar-fill { background: var(--tone-danger-fg); }
.bar-Completed .bar-fill { background: var(--tone-success-fg); }
.today { position: absolute; top: 0; bottom: 0; border-left: 2px solid var(--accent); }
.legend { margin: 8px 0 0; display: flex; align-items: center; gap: 4px; flex-wrap: wrap; }
.legend-bar, .legend-fill, .legend-today { display: inline-block; width: 18px; height: 10px; vertical-align: middle; }
.legend-bar { border: 1px solid var(--border-strong); background: var(--tone-neutral-bg); }
.legend-fill { background: var(--navy-soft); }
.legend-today { width: 2px; height: 14px; background: var(--accent); }
</style>
