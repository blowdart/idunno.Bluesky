import React, { useEffect, useState } from 'react';
import { request } from './api.js';

export default function App() {
  const [session, setSession] = useState(null);
  const [handle, setHandle] = useState('');
  const [text, setText] = useState('');
  const [posts, setPosts] = useState([]);
  const [busy, setBusy] = useState(true);
  const [message, setMessage] = useState('');
  const [error, setError] = useState(
    new URLSearchParams(window.location.search).has('loginError')
      ? 'OAuth login failed or expired. Start login again.'
      : '',
  );

  useEffect(() => {
    let active = true;
    request('/api/session')
      .then(value => { if (active) setSession(value); })
      .catch(reason => { if (active) setError(reason.message); })
      .finally(() => { if (active) setBusy(false); });
    window.history.replaceState(null, '', '/');
    return () => { active = false; };
  }, []);

  async function perform(action) {
    setBusy(true);
    setError('');
    setMessage('');
    try {
      await action();
    } catch (reason) {
      setError(reason.message);
    } finally {
      try {
        const current = await request('/api/session');
        setSession(current);
        if (!current.authenticated) setPosts([]);
      } catch (reason) {
        setError(reason.message);
      }
      setBusy(false);
    }
  }

  function login(event) {
    event.preventDefault();
    perform(async () => {
      const result = await request('/api/login', { method: 'POST', body: { handle } });
      window.location.assign(result.authorizationUrl);
    });
  }

  function createPost(event) {
    event.preventDefault();
    perform(async () => {
      await request('/api/posts', { method: 'POST', body: { text } });
      setText('');
      setMessage('Public post created. Delete it using the button below.');
    });
  }

  return <main>
    <h1>Bluesky React BFF sample</h1>
    <p>OAuth credentials stay on the ASP.NET server. This browser receives only a session cookie and public data.</p>
    {error && <p role="alert">{error}</p>}
    {message && <p role="status">{message}</p>}
    {busy && <p role="status">Working...</p>}
    {!session && !busy && <button onClick={() => window.location.reload()}>Reload session</button>}
    {session && !session.authenticated && <form onSubmit={login}>
      <label htmlFor="handle">Bluesky handle</label>
      <input id="handle" autoComplete="username" required value={handle}
        onChange={event => setHandle(event.target.value)} disabled={busy} />
      <button disabled={busy}>Log in with OAuth</button>
    </form>}
    {session?.authenticated && <>
      <p>Signed in as <code>{session.did}</code>. Session ends at {new Date(session.expiresAt).toLocaleString()}.</p>
      <button disabled={busy} onClick={() => perform(async () => {
        await request('/api/logout', { method: 'POST' });
        setMessage('Signed out locally. Remote token revocation was attempted.');
      })}>Log out</button>
      <section aria-labelledby="posting">
        <h2 id="posting">Create and delete a post</h2>
        <p>Use a test account: this creates a real public post. Logout, expiry and server restarts do not delete it.</p>
        {session.createdPost ? <article>
          <p>{session.createdPost.text}</p>
          <p><code>{session.createdPost.uri}</code></p>
          <button disabled={busy} onClick={() => perform(async () => {
            await request('/api/posts/created', { method: 'DELETE' });
            setMessage('The sample post was deleted.');
          })}>Delete this post</button>
        </article> : <form onSubmit={createPost}>
          <label htmlFor="post">Public post text (up to 300 graphemes)</label>
          <textarea id="post" required value={text} onChange={event => setText(event.target.value)} disabled={busy} />
          <button disabled={busy || !text.trim()}>Create public post</button>
        </form>}
      </section>
      <section aria-labelledby="timeline">
        <h2 id="timeline">Timeline</h2>
        <button disabled={busy} onClick={() => perform(async () => {
          const result = await request('/api/timeline');
          setPosts(result.posts);
          if (!result.posts.length) setMessage('Your timeline is empty.');
        })}>Load timeline</button>
        <ul>{posts.map((post, index) => <li key={`${post.uri}-${index}`}>
          <strong>@{post.author}</strong>
          <p>{post.text}</p>
        </li>)}</ul>
      </section>
    </>}
  </main>;
}
