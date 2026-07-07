import { requestJson } from './http';

function authPayload(email, password) {
  return { email, password };
}

export const authApi = {
  login(payload) {
    return requestJson('/api/auth/login', { method: 'POST', body: authPayload(payload.email, payload.password) });
  },
  register(payload) {
    return requestJson('/api/auth/register', { method: 'POST', body: authPayload(payload.email, payload.password) });
  },
  me(token) {
    return requestJson('/api/auth/me', { token });
  },
};

export const subscriptionsApi = {
  listMine(token) {
    return requestJson('/api/subscriptions/', { token });
  },
  createMine(token, payload) {
    return requestJson('/api/subscriptions/', { method: 'POST', token, body: payload });
  },
  updateMine(token, id, payload) {
    return requestJson(`/api/subscriptions/${id}`, { method: 'PUT', token, body: payload });
  },
  deleteMine(token, id) {
    return requestJson(`/api/subscriptions/${id}`, { method: 'DELETE', token });
  },
  listForUser(token, userId) {
    return requestJson(`/api/admin/users/${userId}/subscriptions`, { token });
  },
  createForUser(token, userId, payload) {
    return requestJson(`/api/admin/users/${userId}/subscriptions`, { method: 'POST', token, body: payload });
  },
  updateForUser(token, userId, id, payload) {
    return requestJson(`/api/admin/users/${userId}/subscriptions/${id}`, {
      method: 'PUT',
      token,
      body: payload,
    });
  },
  deleteForUser(token, userId, id) {
    return requestJson(`/api/admin/users/${userId}/subscriptions/${id}`, { method: 'DELETE', token });
  },
};

export const adminApi = {
  listUsers(token, page = 1, pageSize = 20) {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    return requestJson(`/api/admin/users?${query.toString()}`, { token });
  },
  runAgent(token) {
    return requestJson('/api/admin/agent/run', { method: 'POST', token });
  },
};