CREATE TABLE IF NOT EXISTS label_schema (id INTEGER PRIMARY KEY CHECK(id=1), version INTEGER NOT NULL CHECK(version>0));
CREATE TABLE IF NOT EXISTS label_state (
    id INTEGER PRIMARY KEY CHECK(id=1),
    stream_id VARCHAR(32) COLLATE "C" NOT NULL,
    issuer TEXT COLLATE "C" NOT NULL,
    revision BIGINT NOT NULL DEFAULT 0 CHECK(revision>=0),
    issued BIGINT NOT NULL DEFAULT 0 CHECK(issued>=0),
    published BIGINT NOT NULL DEFAULT 0 CHECK(published>=0 AND published<=issued),
    replay_floor BIGINT NOT NULL DEFAULT 0 CHECK(replay_floor>=0 AND replay_floor<=published),
    phase INTEGER NOT NULL DEFAULT 0 CHECK(phase BETWEEN 0 AND 6),
    retirement_upper BIGINT NOT NULL DEFAULT 0 CHECK(retirement_upper>=0 AND retirement_upper<=issued),
    retirement_through BIGINT NOT NULL DEFAULT 0 CHECK(retirement_through>=0 AND retirement_through<=retirement_upper),
    grace_deadline BIGINT
);
CREATE TABLE IF NOT EXISTS label_entries (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    signed_json BYTEA NOT NULL,
    payload BYTEA NOT NULL,
    format_version INTEGER NOT NULL CHECK(format_version=1),
    queued_at BIGINT NOT NULL
);
CREATE TABLE IF NOT EXISTS label_distribution (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    slot_hash BYTEA NOT NULL CHECK(octet_length(slot_hash)=32),
    slot_key BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_distribution_slot ON label_distribution(slot_hash, seq);
CREATE TABLE IF NOT EXISTS label_operations (
    operation_hash BYTEA PRIMARY KEY CHECK(octet_length(operation_hash)=32),
    operation_id TEXT COLLATE "C" NOT NULL,
    first_seq BIGINT NOT NULL CHECK(first_seq>=0),
    last_seq BIGINT NOT NULL CHECK(last_seq>=first_seq),
    revision BIGINT NOT NULL CHECK(revision>0),
    request BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_operations_revision ON label_operations(revision);
CREATE TABLE IF NOT EXISTS label_decisions (
    operation_hash BYTEA PRIMARY KEY CHECK(octet_length(operation_hash)=32),
    operation_id TEXT COLLATE "C" NOT NULL,
    revision BIGINT NOT NULL CHECK(revision>0),
    receipt BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_decisions_revision ON label_decisions(revision);
INSERT INTO label_state(id, stream_id, issuer) VALUES(1, '00000000000000000000000000000000', '') ON CONFLICT DO NOTHING;
CREATE TABLE IF NOT EXISTS label_team (id INTEGER PRIMARY KEY CHECK(id=1), payload BYTEA NOT NULL);
CREATE TABLE IF NOT EXISTS label_admin_audit (seq BIGINT PRIMARY KEY, payload BYTEA NOT NULL);
CREATE TABLE IF NOT EXISTS label_admin_receipts (operation_hash BYTEA PRIMARY KEY, operation_id TEXT COLLATE "C" NOT NULL, intent BYTEA NOT NULL, payload BYTEA NOT NULL);
CREATE TABLE IF NOT EXISTS label_admin_tokens (replay_hash BYTEA PRIMARY KEY, token_hash TEXT COLLATE "C" NOT NULL, actor TEXT COLLATE "C" NOT NULL, expires_ms BIGINT NOT NULL);
CREATE INDEX IF NOT EXISTS label_admin_token_hash ON label_admin_tokens(token_hash);
INSERT INTO label_schema(id, version) VALUES(1, 4) ON CONFLICT DO NOTHING;
