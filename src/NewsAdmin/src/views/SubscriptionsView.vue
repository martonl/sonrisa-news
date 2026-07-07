<template>
  <section class="stack">
    <div class="hero-card section-header">
      <div>
        <p class="eyebrow">Subscriptions</p>
        <h2>Manage your delivery targets.</h2>
      </div>
      <button class="ghost-button" type="button" @click="loadSubscriptions">Refresh</button>
    </div>

    <div class="panel">
      <form class="subscription-form" @submit.prevent="createSubscription">
        <label>
          <span>Type</span>
          <select v-model="draft.type">
            <option value="Email">Email</option>
            <option value="Slack">Slack</option>
          </select>
        </label>

        <label class="grow">
          <span>Target</span>
          <input v-model.trim="draft.target" type="text" placeholder="name@company.com or #alerts" required />
        </label>

        <button class="primary-button" type="submit" :disabled="busy">Add subscription</button>
      </form>
    </div>

    <p v-if="error" class="status status-error">{{ error }}</p>

    <div class="cards-grid">
      <article v-for="subscription in subscriptions" :key="subscription.id" class="panel subscription-card">
        <div class="subscription-head">
          <div>
            <p class="eyebrow">{{ subscription.type }}</p>
            <h3>{{ subscription.target }}</h3>
          </div>
          <span :class="['pill', subscription.isActive ? 'pill-on' : 'pill-off']">
            {{ subscription.isActive ? 'Active' : 'Paused' }}
          </span>
        </div>

        <p class="muted">Created {{ formatDate(subscription.createdAt) }}</p>

        <form class="inline-actions" @submit.prevent="saveSubscription(subscription)">
          <label class="grow">
            <span>Target</span>
            <input v-model.trim="subscription.target" type="text" required />
          </label>

          <label class="switch">
            <input v-model="subscription.isActive" type="checkbox" />
            <span>Enabled</span>
          </label>

          <button class="ghost-button" type="submit">Save</button>
          <button class="danger-button" type="button" @click="removeSubscription(subscription.id)">Delete</button>
        </form>
      </article>
    </div>

    <p v-if="!subscriptions.length && !loading" class="empty-state">No subscriptions yet.</p>
  </section>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue';
import { authStore as auth } from '../stores/authStore';
import { subscriptionsApi } from '../api/newsApi';

const subscriptions = ref([]);
const loading = ref(false);
const busy = ref(false);
const error = ref('');
const draft = reactive({ type: 'Email', target: '' });

onMounted(loadSubscriptions);

function formatDate(value) {
  return new Intl.DateTimeFormat('en', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

async function loadSubscriptions() {
  loading.value = true;
  error.value = '';

  try {
    subscriptions.value = await subscriptionsApi.listMine(auth.token);
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to load subscriptions.';
  } finally {
    loading.value = false;
  }
}

async function createSubscription() {
  busy.value = true;
  error.value = '';

  try {
    await subscriptionsApi.createMine(auth.token, { type: draft.type, target: draft.target });
    draft.target = '';
    await loadSubscriptions();
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to create subscription.';
  } finally {
    busy.value = false;
  }
}

async function saveSubscription(subscription) {
  error.value = '';

  try {
    await subscriptionsApi.updateMine(auth.token, subscription.id, {
      target: subscription.target,
      isActive: subscription.isActive,
    });
    await loadSubscriptions();
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to save subscription.';
  }
}

async function removeSubscription(subscriptionId) {
  error.value = '';

  try {
    await subscriptionsApi.deleteMine(auth.token, subscriptionId);
    await loadSubscriptions();
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to delete subscription.';
  }
}
</script>