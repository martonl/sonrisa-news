<template>
  <div class="app-shell">
    <div class="ambient ambient-one"></div>
    <div class="ambient ambient-two"></div>

    <header class="topbar">
      <div>
        <p class="eyebrow">Sonrisa News</p>
        <h1>Admin console</h1>
      </div>

      <nav v-if="auth.ready" class="topbar-nav">
        <RouterLink v-if="auth.isAuthenticated" to="/dashboard">Dashboard</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/subscriptions">Subscriptions</RouterLink>
        <RouterLink v-if="auth.isAdmin" to="/users">Users</RouterLink>
        <button v-if="auth.isAuthenticated" class="ghost-button" type="button" @click="logout">
          Sign out
        </button>
        <RouterLink v-else class="ghost-button link-button" to="/login">Sign in</RouterLink>
      </nav>
    </header>

    <main class="page-frame">
      <RouterView />
    </main>
  </div>
</template>

<script setup>
import { RouterLink, RouterView } from 'vue-router';
import { authStore as auth } from './stores/authStore';

function logout() {
  auth.logout();
}
</script>