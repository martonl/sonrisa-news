<template>
  <section class="hero-card auth-grid">
    <div class="hero-copy">
      <p class="eyebrow">Secure access</p>
      <h2>Sign in to manage users, subscriptions, and the news agent.</h2>
      <p class="lede">
        JWT auth is stored locally so the admin console reopens straight into the right role.
      </p>
      <ul class="feature-list">
        <li>Login and registration with a single API contract</li>
        <li>Role-aware navigation and route protection</li>
        <li>Subscriptions for current users and admins</li>
      </ul>
    </div>

    <div class="panel auth-panel">
      <div class="auth-tabs">
        <button :class="['tab', mode === 'login' && 'active']" type="button" @click="mode = 'login'">
          Login
        </button>
        <button :class="['tab', mode === 'register' && 'active']" type="button" @click="mode = 'register'">
          Register
        </button>
      </div>

      <form class="form-stack" @submit.prevent="submit">
        <label>
          <span>Email</span>
          <input v-model.trim="email" type="email" autocomplete="email" placeholder="admin@example.com" required />
        </label>

        <label>
          <span>Password</span>
          <input v-model="password" type="password" autocomplete="current-password" placeholder="••••••••" required />
        </label>

        <p v-if="error" class="status status-error">{{ error }}</p>
        <p v-else class="status">Use the seeded admin account or register a new user.</p>

        <button class="primary-button" type="submit" :disabled="busy">
          {{ busy ? 'Working…' : mode === 'login' ? 'Sign in' : 'Create account' }}
        </button>
      </form>
    </div>
  </section>
</template>

<script setup>
import { ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { authStore } from '../stores/authStore';

const router = useRouter();
const route = useRoute();

const mode = ref('login');
const email = ref('');
const password = ref('');
const error = ref('');
const busy = ref(false);

async function submit() {
  error.value = '';
  busy.value = true;

  try {
    if (mode.value === 'login') {
      await authStore.login(email.value, password.value);
    } else {
      await authStore.register(email.value, password.value);
    }

    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard';
    await router.replace(redirect);
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Authentication failed.';
  } finally {
    busy.value = false;
  }
}
</script>