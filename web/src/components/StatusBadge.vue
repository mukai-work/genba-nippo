<script setup lang="ts">
import type { Tone } from '@/domain/labels'

defineProps<{ label: string; tone: Tone }>()
</script>

<template>
  <span class="badge" :class="`tone-${tone}`">{{ label }}</span>
</template>
