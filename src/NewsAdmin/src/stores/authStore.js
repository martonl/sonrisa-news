import { reactive, computed } from 'vue';
import { authApi } from '../api/newsApi';

const STORAGE_TOKEN = 'sonrisa-news.jwt';
const STORAGE_USER = 'sonrisa-news.user';

const state = reactive({
  token: localStorage.getItem(STORAGE_TOKEN) ?? '',
  user: readUser(),
  ready: false,
  status: 'idle',
  error: '',
});

function readUser() {
  const raw = localStorage.getItem(STORAGE_USER);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function persistSession(token, user) {
  state.token = token;
  state.user = user;
  localStorage.setItem(STORAGE_TOKEN, token);
  localStorage.setItem(STORAGE_USER, JSON.stringify(user));
}

function clearSession() {
  state.token = '';
  state.user = null;
  localStorage.removeItem(STORAGE_TOKEN);
  localStorage.removeItem(STORAGE_USER);
}

async function refreshCurrentUser() {
  if (!state.token) {
    state.user = null;
    return null;
  }

  const user = await authApi.me(state.token);
  state.user = user;
  localStorage.setItem(STORAGE_USER, JSON.stringify(user));
  return user;
}

async function hydrate() {
  if (state.ready) {
    return;
  }

  state.ready = true;

  if (!state.token) {
    clearSession();
    return;
  }

  try {
    await refreshCurrentUser();
  } catch {
    clearSession();
  }
}

async function login(email, password) {
  state.status = 'loading';
  state.error = '';

  try {
    const result = await authApi.login({ email, password });
    persistSession(result.token, null);
    await refreshCurrentUser();
    return true;
  } catch (error) {
    state.error = error instanceof Error ? error.message : 'Login failed.';
    throw error;
  } finally {
    state.status = 'idle';
  }
}

async function register(email, password) {
  state.status = 'loading';
  state.error = '';

  try {
    const result = await authApi.register({ email, password });
    persistSession(result.token, null);
    await refreshCurrentUser();
    return true;
  } catch (error) {
    state.error = error instanceof Error ? error.message : 'Registration failed.';
    throw error;
  } finally {
    state.status = 'idle';
  }
}

function logout() {
  clearSession();
}

const isAuthenticated = computed(() => Boolean(state.token && state.user));
const isAdmin = computed(() => Boolean(state.user?.roles?.includes('Admin')));

export const authStore = {
  state,
  get token() {
    return state.token;
  },
  get user() {
    return state.user;
  },
  get ready() {
    return state.ready;
  },
  get status() {
    return state.status;
  },
  get error() {
    return state.error;
  },
  get isAuthenticated() {
    return isAuthenticated.value;
  },
  get isAdmin() {
    return isAdmin.value;
  },
  hydrate,
  login,
  register,
  logout,
  refreshCurrentUser,
};