<template>
  <section class="stack">
    <div class="hero-card section-header">
      <div>
        <p class="eyebrow">Users</p>
        <h2>Admin user management.</h2>
      </div>

      <div class="inline-actions compact">
        <button class="ghost-button" type="button" @click="previousPage" :disabled="page === 1 || loading">
          Previous
        </button>
        <button class="ghost-button" type="button" @click="nextPage" :disabled="loading || !hasMore">
          Next
        </button>
      </div>
    </div>

    <div class="panel">
      <div class="users-summary">
        <p>Total users: {{ totalCount }}</p>
        <p>Page {{ page }} of {{ totalPages || 1 }}</p>
      </div>
    </div>

    <p v-if="error" class="status status-error">{{ error }}</p>

    <div class="cards-grid users-grid">
      <article v-for="user in users" :key="user.id" class="panel user-card">
        <button class="user-card-button" type="button" @click="selectUser(user)">
          <div>
            <p class="eyebrow">User</p>
            <h3>{{ user.email }}</h3>
          </div>
          <span class="pill pill-on">Open</span>
        </button>
      </article>
    </div>

    <section v-if="selectedUser" class="stack">
      <div class="hero-card section-header">
        <div>
          <p class="eyebrow">Selected user</p>
          <h2>{{ selectedUser.email }}</h2>
        </div>
        <button class="ghost-button" type="button" @click="selectedUser = null">Close</button>
      </div>

      <div class="panel">
        <form class="subscription-form" @submit.prevent="createSubscriptionForSelected">
          <label>
            <span>Type</span>
            <select v-model="selectedDraft.type">
              <option value="Email">Email</option>
              <option value="Slack">Slack</option>
            </select>
          </label>

          <label class="grow">
            <span>Target</span>
            <input v-model.trim="selectedDraft.target" type="text" placeholder="alerts@company.com" required />
          </label>

          <button class="primary-button" type="submit" :disabled="selectedBusy">Create for user</button>
        </form>
      </div>

      <div class="cards-grid">
        <article v-for="subscription in selectedSubscriptions" :key="subscription.id" class="panel subscription-card">
          <div class="subscription-head">
            <div>
              <p class="eyebrow">{{ subscription.type }}</p>
              <h3>{{ subscription.target }}</h3>
            </div>
            <span :class="['pill', subscription.isActive ? 'pill-on' : 'pill-off']">
              {{ subscription.isActive ? 'Active' : 'Paused' }}
            </span>
          </div>

          <form class="inline-actions" @submit.prevent="saveSelectedSubscription(subscription)">
            <label class="grow">
              <span>Target</span>
              <input v-model.trim="subscription.target" type="text" required />
            </label>

            <label class="switch">
              <input v-model="subscription.isActive" type="checkbox" />
              <span>Enabled</span>
            </label>

            <button class="ghost-button" type="submit">Save</button>
            <button class="danger-button" type="button" @click="deleteSelectedSubscription(subscription.id)">Delete</button>
          </form>
        </article>
      </div>
    </section>
  </section>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { authStore as auth } from '../stores/authStore';
import { adminApi, subscriptionsApi } from '../api/newsApi';

const users = ref([]);
const selectedUser = ref(null);
const selectedSubscriptions = ref([]);
const page = ref(1);
const pageSize = 8;
const totalCount = ref(0);
const loading = ref(false);
const selectedBusy = ref(false);
const error = ref('');
const selectedDraft = reactive({ type: 'Email', target: '' });

const totalPages = computed(() => Math.ceil(totalCount.value / pageSize));
const hasMore = computed(() => page.value * pageSize < totalCount.value);

onMounted(loadUsers);

async function loadUsers() {
  loading.value = true;
  error.value = '';

  try {
    const result = await adminApi.listUsers(auth.token, page.value, pageSize);
    users.value = result.items;
    totalCount.value = result.totalCount;

    if (selectedUser.value) {
      await loadSelectedSubscriptions(selectedUser.value.id);
    }
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to load users.';
  } finally {
    loading.value = false;
  }
}

function nextPage() {
  if (hasMore.value) {
    page.value += 1;
    loadUsers();
  }
}

function previousPage() {
  if (page.value > 1) {
    page.value -= 1;
    loadUsers();
  }
}

async function selectUser(user) {
  selectedUser.value = user;
  await loadSelectedSubscriptions(user.id);
}

async function loadSelectedSubscriptions(userId) {
  selectedBusy.value = true;

  try {
    selectedSubscriptions.value = await subscriptionsApi.listForUser(auth.token, userId);
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to load user subscriptions.';
  } finally {
    selectedBusy.value = false;
  }
}

async function createSubscriptionForSelected() {
  if (!selectedUser.value) {
    return;
  }

  selectedBusy.value = true;
  error.value = '';

  try {
    await subscriptionsApi.createForUser(auth.token, selectedUser.value.id, {
      type: selectedDraft.type,
      target: selectedDraft.target,
    });
    selectedDraft.target = '';
    await loadSelectedSubscriptions(selectedUser.value.id);
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to create subscription.';
  } finally {
    selectedBusy.value = false;
  }
}

async function saveSelectedSubscription(subscription) {
  if (!selectedUser.value) {
    return;
  }

  error.value = '';

  try {
    await subscriptionsApi.updateForUser(auth.token, selectedUser.value.id, subscription.id, {
      target: subscription.target,
      isActive: subscription.isActive,
    });
    await loadSelectedSubscriptions(selectedUser.value.id);
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to save subscription.';
  }
}

async function deleteSelectedSubscription(subscriptionId) {
  if (!selectedUser.value) {
    return;
  }

  error.value = '';

  try {
    await subscriptionsApi.deleteForUser(auth.token, selectedUser.value.id, subscriptionId);
    await loadSelectedSubscriptions(selectedUser.value.id);
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Failed to delete subscription.';
  }
}
</script>