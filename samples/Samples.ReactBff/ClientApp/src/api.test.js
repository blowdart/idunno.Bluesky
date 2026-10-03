import { afterEach, describe, expect, it, vi } from 'vitest';
import { request } from './api.js';

afterEach(() => vi.unstubAllGlobals());

describe('same-origin BFF requests', () => {
  it.each(['POST', 'DELETE'])('obtains a fresh CSRF token for %s', async method => {
    const fetch = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'csrf-token' })))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetch);
    await request('/api/posts/created', { method });
    expect(fetch.mock.calls[0][0]).toBe('/api/csrf');
    expect(fetch.mock.calls[1][1]).toMatchObject({
      method,
      credentials: 'same-origin',
      cache: 'no-store',
      redirect: 'error',
      headers: { 'X-CSRF-TOKEN': 'csrf-token' },
    });
    expect(fetch.mock.calls[1][1].headers.Authorization).toBeUndefined();
  });

  it('sends JSON text, not credentials, to create a post', async () => {
    const fetch = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'csrf-token' })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ uri: 'at://post', text: 'Hello' })));
    vi.stubGlobal('fetch', fetch);
    await request('/api/posts', { method: 'POST', body: { text: 'Hello' } });
    expect(fetch.mock.calls[1][1].body).toBe('{"text":"Hello"}');
    expect(fetch.mock.calls[1][1].headers['Content-Type']).toBe('application/json');
  });

  it('surfaces server errors and never retries writes', async () => {
    const fetch = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'csrf-token' })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ error: 'Scope missing.' }), {
        status: 502, headers: { 'content-type': 'application/json' },
      }));
    vi.stubGlobal('fetch', fetch);
    await expect(request('/api/posts', { method: 'POST', body: { text: 'Hello' } })).rejects.toThrow('Scope missing.');
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('does not send a mutation when CSRF acquisition fails', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response('', { status: 503 }));
    vi.stubGlobal('fetch', fetch);
    await expect(request('/api/logout', { method: 'POST' })).rejects.toThrow('HTTP 503');
    expect(fetch).toHaveBeenCalledTimes(1);
  });
});
