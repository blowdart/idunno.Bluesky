CREATE TABLE IF NOT EXISTS "idunno_bluesky_identities"
(
    "Did" VARCHAR(2048) COLLATE "C" NOT NULL,
    "Identity" BYTEA NOT NULL,
    "ExpiresAtUtc" TIMESTAMPTZ NOT NULL,
    PRIMARY KEY ("Did")
);

CREATE INDEX IF NOT EXISTS "IX_idunno_bluesky_identities_ExpiresAtUtc"
    ON "idunno_bluesky_identities" ("ExpiresAtUtc");

CREATE TABLE IF NOT EXISTS "idunno_bluesky_refresh_locks"
(
    "Did" VARCHAR(2048) COLLATE "C" NOT NULL,
    "LockToken" VARCHAR(32) COLLATE "C" NOT NULL,
    "ExpiresAtUtc" TIMESTAMPTZ NOT NULL,
    PRIMARY KEY ("Did")
);

CREATE INDEX IF NOT EXISTS "IX_idunno_bluesky_refresh_locks_ExpiresAtUtc"
    ON "idunno_bluesky_refresh_locks" ("ExpiresAtUtc");

CREATE TABLE IF NOT EXISTS "idunno_bluesky_correlation_states"
(
    "CorrelationId" UUID NOT NULL,
    "State" TEXT NOT NULL,
    "ExpiresAtUtc" TIMESTAMPTZ NOT NULL,
    PRIMARY KEY ("CorrelationId")
);

CREATE INDEX IF NOT EXISTS "IX_idunno_bluesky_correlation_states_ExpiresAtUtc"
    ON "idunno_bluesky_correlation_states" ("ExpiresAtUtc");
