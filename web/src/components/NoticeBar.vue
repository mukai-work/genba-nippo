<script setup lang="ts">
import type { Tone } from '@/domain/labels'

defineProps<{ message: string; tone?: Tone }>()
</script>

<template>
  <div class="notice" :class="`tone-${tone ?? 'danger'}`" role="alert">
    <span>{{ message }}</span>
    <slot />
  </div>
</template>
