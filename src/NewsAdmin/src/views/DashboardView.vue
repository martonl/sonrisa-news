<template>
  <section class="stack">
    <div class="hero-card dashboard-hero">
      <div>
        <p class="eyebrow">Dashboard</p>
        <h2>Operational overview for the news platform.</h2>
        <p class="lede">
          The current session is authenticated as {{ auth.user?.email ?? 'unknown user' }}.
        </p>
      </div>

      <div class="hero-metrics">
        <article class="metric-card">
          <span>Role</span>
          <strong>{{ auth.isAdmin ? 'Admin' : 'User' }}</strong>
        </article>
        <article class="metric-card">
          <span>Token</span>
          <strong>{{ auth.token ? 'Stored' : 'Missing' }}</strong>
        </article>
      </div>
    </div>

    <div class="panel action-panel">
      <div>
        <h3>News agent</h3>
        <p>Admins can manually trigger the evaluator to process the latest feed window.</p>
      </div>

      <button class="primary-button" type="button" :disabled="running || !auth.isAdmin" @click="triggerAgent">
        {{ running ? 'Triggering…' : auth.isAdmin ? 'Run agent now' : 'Admin only' }}
      </button>
    </div>

    <p v-if="message" class="status" :class="messageType">{{ message }}</p>
  </section>
</template>

<script setup>
import { ref } from 'vue';
import { authStore as auth } from '../stores/authStore';
import { adminApi } from '../api/newsApi';

const running = ref(false);
const message = ref('');
const messageType = ref('');

async function triggerAgent() {
  running.value = true;
  message.value = '';

  try {
    await adminApi.runAgent(auth.token);
    messageType.value = 'status-success';
    message.value = 'Agent run completed successfully.';
  } catch (exception) {
    messageType.value = 'status-error';
    message.value = exception instanceof Error ? exception.message : 'Agent run failed.';
  } finally {
    running.value = false;
  }
}
</script>