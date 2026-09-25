CREATE TABLE IF NOT EXISTS "idunno_bluesky_identities"
(
    "Did" TEXT NOT NULL COLLATE BINARY,
    "Identity" BLOB NOT NULL,
    "ExpiresAtUtcTicks" INTEGER NOT NULL,
    PRIMARY KEY ("Did")
) WITHOUT ROWID;

CREATE INDEX IF NOT EXISTS "IX_idunno_bluesky_identities_ExpiresAtUtcTicks"
    ON "idunno_bluesky_identities" ("ExpiresAtUtcTicks");

CREATE TABLE IF NOT EXISTS "idunno_bluesky_refresh_locks"
(
    "Did" TEXT NOT NULL COLLATE BINARY,
    "LockToken" TEXT NOT NULL COLLATE BINARY,
    "ExpiresAtUtcTicks" INTEGER NOT NULL,
    PRIMARY KEY ("Did")
) WITHOUT ROWID;

CREATE INDEX IF NOT EXISTS "IX_idunno_bluesky_refresh_locks_ExpiresAtUtcTicks"
    ON "idunno_bluesky_refresh_locks" ("ExpiresAtUtcTicks");

CREATE TABLE IF NOT EXISTS "idunno_bluesky_correlation_states"
(
    "CorrelationId" BLOB NOT NULL,
    "State" TEXT NOT NULL,
    "ExpiresAtUtcTicks" INTEGER NOT NULL,
    PRIMARY KEY ("CorrelationId")
) WITHOUT ROWID;

CREATE INDEX IF NOT EXISTS "IX_idunno_bluesky_correlation_states_ExpiresAtUtcTicks"
    ON "idunno_bluesky_correlation_states" ("ExpiresAtUtcTicks");
