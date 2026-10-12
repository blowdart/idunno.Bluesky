// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

// Interactive Server events don't produce HTTP responses that can renew authentication cookies.
// Send real HTTP requests while the user is active, then pass the protected expiry receipt to
// the circuit so it doesn't continue enforcing the cookie's original deadline.
export function start(component) {
    const interval = 60_000;
    let lastActivity = Date.now();
    let lastAttempt = 0;
    let pending = false;
    let stopped = false;
    const controller = new AbortController();
    const activityEvents = ["pointerdown", "keydown", "wheel", "touchstart"];

    async function renew() {
        const now = Date.now();
        // An open tab alone isn't activity. Rate limiting and a single in-flight request prevent
        // input events from flooding the server, while hidden or idle tabs are allowed to expire.
        if (stopped || pending || document.hidden || now - lastActivity > interval ||
            now - lastAttempt < interval) {
            return;
        }

        pending = true;
        lastAttempt = now;
        try {
            const token = document.querySelector('meta[name="session-antiforgery"]')?.content;
            if (!token) {
                stopped = true;
                await component.invokeMethodAsync("RenewalFailed", true);
                return;
            }

            const response = await fetch("/session/renew", {
                method: "POST",
                credentials: "same-origin",
                headers: { RequestVerificationToken: token },
                redirect: "error",
                cache: "no-store",
                signal: controller.signal
            });
            if (controller.signal.aborted) {
                return;
            }

            if (response.ok) {
                // The browser applies Set-Cookie; only the opaque receipt crosses back into .NET.
                // If the circuit rejects it, stop rather than treating a different login as renewal.
                stopped = !await component.invokeMethodAsync("RenewalSucceeded", await response.text());
            } else {
                // Authentication or antiforgery failures need sign-in/reload, not endless retries.
                // Transient server and network failures can retry on subsequent activity.
                stopped = response.status === 401 || response.status === 403 || response.status === 400;
                await component.invokeMethodAsync("RenewalFailed", stopped);
            }
        } catch (error) {
            if (!controller.signal.aborted) {
                console.error("Session renewal failed.", error);
                try {
                    await component.invokeMethodAsync("RenewalFailed", false);
                } catch (notificationError) {
                    console.error("Could not notify the disconnected circuit about session renewal.", notificationError);
                }
            }
        } finally {
            pending = false;
        }
    }

    function activity() {
        lastActivity = Date.now();
        void renew();
    }

    function visible() {
        if (!document.hidden) {
            // Switching back is activity even when the tab has been idle for hours. Check now
            // instead of waiting for the first edit to discover that the session has expired.
            lastActivity = Date.now();
            void renew();
        }
    }

    for (const event of activityEvents) {
        document.addEventListener(event, activity, { passive: true });
    }
    document.addEventListener("visibilitychange", visible);
    const timer = setInterval(() => void renew(), interval);
    void renew();

    return {
        dispose() {
            // The layout owns this heartbeat. Removing it must also remove listeners, timers and
            // pending fetches so a retired circuit can't keep renewing the browser's session.
            stopped = true;
            controller.abort();
            clearInterval(timer);
            for (const event of activityEvents) {
                document.removeEventListener(event, activity);
            }
            document.removeEventListener("visibilitychange", visible);
        }
    };
}
