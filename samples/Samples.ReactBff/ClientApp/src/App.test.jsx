import React from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import App from './App.jsx';
import { request } from './api.js';

vi.mock('./api.js', () => ({ request: vi.fn() }));
afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

const signedIn = {
  authenticated: true,
  did: 'did:plc:tester',
  expiresAt: '2026-10-03T22:00:00Z',
  createdPost: null,
};

it('renders OAuth login for an anonymous session', async () => {
  request.mockResolvedValue({ authenticated: false });
  render(<App />);
  expect(await screen.findByRole('button', { name: 'Log in with OAuth' })).toBeDefined();
  expect(screen.queryByRole('button', { name: 'Create public post' })).toBeNull();
});

it('creates a post then deletes only the server-tracked post', async () => {
  let session = { ...signedIn };
  request.mockImplementation(async (path, options) => {
    if (path === '/api/session') return session;
    if (path === '/api/posts') {
      session = { ...session, createdPost: { uri: 'at://sample/post/1', text: options.body.text } };
      return session.createdPost;
    }
    if (path === '/api/posts/created') {
      session = { ...session, createdPost: null };
      return null;
    }
    throw new Error('Unexpected endpoint.');
  });
  render(<App />);
  fireEvent.change(await screen.findByLabelText('Public post text (up to 300 graphemes)'), { target: { value: 'Public test post' } });
  fireEvent.click(screen.getByRole('button', { name: 'Create public post' }));
  const deletion = await screen.findByRole('button', { name: 'Delete this post' });
  expect(screen.getByText('at://sample/post/1')).toBeDefined();
  fireEvent.click(deletion);
  await screen.findByRole('button', { name: 'Create public post' });
  expect(request).toHaveBeenCalledWith('/api/posts', { method: 'POST', body: { text: 'Public test post' } });
  expect(request).toHaveBeenCalledWith('/api/posts/created', { method: 'DELETE' });
});

it('renders timeline text without interpreting HTML', async () => {
  request.mockImplementation(async path => path === '/api/session'
    ? signedIn
    : { posts: [{ uri: 'at://sample/1', author: 'test.example', text: '<script>bad()</script>' }] });
  const { container } = render(<App />);
  fireEvent.click(await screen.findByRole('button', { name: 'Load timeline' }));
  expect(await screen.findByText('<script>bad()</script>')).toBeDefined();
  expect(container.querySelector('script')).toBeNull();
});

it('reports logout failure while showing that the local session ended', async () => {
  let session = signedIn;
  request.mockImplementation(async path => {
    if (path === '/api/session') return session;
    session = { authenticated: false };
    throw new Error('Remote revocation failed.');
  });
  render(<App />);
  fireEvent.click(await screen.findByRole('button', { name: 'Log out' }));
  await waitFor(() => expect(screen.getByRole('alert').textContent).toBe('Remote revocation failed.'));
  expect(await screen.findByRole('button', { name: 'Log in with OAuth' })).toBeDefined();
});
