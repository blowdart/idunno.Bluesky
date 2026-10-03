export async function request(path, { method = 'GET', body } = {}) {
  const headers = {};
  if (method !== 'GET') {
    const csrf = await request('/api/csrf');
    headers['X-CSRF-TOKEN'] = csrf.token;
  }
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const response = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: 'same-origin',
    cache: 'no-store',
    redirect: 'error',
  });
  if (!response.ok) {
    const error = response.headers.get('content-type')?.includes('application/json')
      ? await response.json()
      : null;
    throw new Error(error?.error ?? `Request failed (HTTP ${response.status}).`);
  }
  return response.status === 204 ? null : response.json();
}
