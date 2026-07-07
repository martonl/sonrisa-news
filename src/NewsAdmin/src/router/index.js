import { createRouter, createWebHistory } from 'vue-router';
import { authStore } from '../stores/authStore';
import LoginView from '../views/LoginView.vue';
import DashboardView from '../views/DashboardView.vue';
import UsersView from '../views/UsersView.vue';
import SubscriptionsView from '../views/SubscriptionsView.vue';

const routes = [
  { path: '/', redirect: '/dashboard' },
  { path: '/login', component: LoginView, meta: { anonymousOnly: true } },
  { path: '/dashboard', component: DashboardView, meta: { requiresAuth: true } },
  { path: '/subscriptions', component: SubscriptionsView, meta: { requiresAuth: true } },
  { path: '/users', component: UsersView, meta: { requiresAuth: true, requiresAdmin: true } },
  { path: '/:pathMatch(.*)*', redirect: '/dashboard' },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior() {
    return { top: 0 };
  },
});

router.beforeEach(async (to) => {
  await authStore.hydrate();

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return { path: '/login', query: { redirect: to.fullPath } };
  }

  if (to.meta.requiresAdmin && !authStore.isAdmin) {
    return { path: '/dashboard' };
  }

  if (to.meta.anonymousOnly && authStore.isAuthenticated) {
    return { path: '/dashboard' };
  }

  return true;
});

export default router;