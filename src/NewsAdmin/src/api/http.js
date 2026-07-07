function parseErrorMessage(body, fallback) {
  if (!body) {
    return fallback;
  }

  if (typeof body === 'string') {
    return body;
  }

  if (Array.isArray(body)) {
    return body
      .map((entry) => entry?.description ?? entry?.message ?? entry?.title ?? '')
      .filter(Boolean)
      .join(', ') || fallback;
  }

  return body.detail || body.message || body.title || fallback;
}

async function readBody(response) {
  const contentType = response.headers.get('content-type') ?? '';

  if (response.status === 204) {
    return null;
  }

  if (contentType.includes('application/json')) {
    return response.json();
  }

  const text = await response.text();
  return text ? text : null;
}

export async function requestJson(path, { method = 'GET', token, body } = {}) {
  const headers = {
    Accept: 'application/json',
  };

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  const payload = await readBody(response);

  if (!response.ok) {
    throw new Error(parseErrorMessage(payload, `Request failed with status ${response.status}.`));
  }

  return payload;
}