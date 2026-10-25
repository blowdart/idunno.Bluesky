CREATE TABLE IF NOT EXISTS label_schema (id INTEGER PRIMARY KEY CHECK(id=1), version INTEGER NOT NULL CHECK(version>0));
CREATE TABLE IF NOT EXISTS label_state (
    id INTEGER PRIMARY KEY CHECK(id=1),
    stream_id TEXT NOT NULL,
    issuer TEXT NOT NULL,
    revision INTEGER NOT NULL DEFAULT 0 CHECK(revision>=0),
    issued INTEGER NOT NULL DEFAULT 0 CHECK(issued>=0),
    published INTEGER NOT NULL DEFAULT 0 CHECK(published>=0 AND published<=issued),
    replay_floor INTEGER NOT NULL DEFAULT 0 CHECK(replay_floor>=0 AND replay_floor<=published),
    phase INTEGER NOT NULL DEFAULT 0 CHECK(phase BETWEEN 0 AND 6),
    retirement_upper INTEGER NOT NULL DEFAULT 0 CHECK(retirement_upper>=0 AND retirement_upper<=issued),
    retirement_through INTEGER NOT NULL DEFAULT 0 CHECK(retirement_through>=0 AND retirement_through<=retirement_upper),
    grace_deadline INTEGER
);
CREATE TABLE IF NOT EXISTS label_entries (
    seq INTEGER PRIMARY KEY CHECK(seq>0),
    signed_json BLOB NOT NULL,
    payload BLOB NOT NULL,
    format_version INTEGER NOT NULL CHECK(format_version=1),
    queued_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS label_distribution (
    seq INTEGER PRIMARY KEY CHECK(seq>0),
    slot_hash BLOB NOT NULL CHECK(length(slot_hash)=32),
    slot_key BLOB NOT NULL
);
CREATE INDEX IF NOT EXISTS label_distribution_slot ON label_distribution(slot_hash, seq);
CREATE TABLE IF NOT EXISTS label_decision_index (
    seq INTEGER PRIMARY KEY CHECK(seq>0),
    identity_hash BLOB NOT NULL CHECK(length(identity_hash)=32),
    identity_key BLOB NOT NULL
);
CREATE INDEX IF NOT EXISTS label_decision_index_identity ON label_decision_index(identity_hash, seq);
CREATE TABLE IF NOT EXISTS label_operations (
    operation_hash BLOB PRIMARY KEY CHECK(length(operation_hash)=32),
    operation_id TEXT NOT NULL,
    first_seq INTEGER NOT NULL CHECK(first_seq>=0),
    last_seq INTEGER NOT NULL CHECK(last_seq>=first_seq),
    revision INTEGER NOT NULL CHECK(revision>0),
    request BLOB NOT NULL
);
CREATE INDEX IF NOT EXISTS label_operations_revision ON label_operations(revision);
CREATE TABLE IF NOT EXISTS label_decisions (
    operation_hash BLOB PRIMARY KEY CHECK(length(operation_hash)=32),
    operation_id TEXT NOT NULL,
    revision INTEGER NOT NULL CHECK(revision>0),
    receipt BLOB NOT NULL
);
CREATE INDEX IF NOT EXISTS label_decisions_revision ON label_decisions(revision);
INSERT OR IGNORE INTO label_state(id, stream_id, issuer) VALUES(1, '00000000000000000000000000000000', '');
CREATE TABLE IF NOT EXISTS label_team (id INTEGER PRIMARY KEY CHECK(id=1), payload BLOB NOT NULL);
CREATE TABLE IF NOT EXISTS label_admin_audit (seq INTEGER PRIMARY KEY, payload BLOB NOT NULL);
CREATE TABLE IF NOT EXISTS label_admin_receipts (operation_hash BLOB PRIMARY KEY, operation_id TEXT NOT NULL, intent BLOB NOT NULL, payload BLOB NOT NULL);
CREATE TABLE IF NOT EXISTS label_admin_tokens (replay_hash BLOB PRIMARY KEY, token_hash TEXT NOT NULL, actor TEXT NOT NULL, expires_ms INTEGER NOT NULL);
CREATE INDEX IF NOT EXISTS label_admin_token_hash ON label_admin_tokens(token_hash);
CREATE INDEX IF NOT EXISTS label_admin_token_expiry ON label_admin_tokens(expires_ms);
CREATE TABLE IF NOT EXISTS label_maintenance (
    id INTEGER PRIMARY KEY CHECK(id=1),
    operation_id TEXT,
    phase INTEGER NOT NULL DEFAULT 0 CHECK(phase BETWEEN 0 AND 5),
    stream_id TEXT NOT NULL,
    previous_key_id TEXT,
    replacement_key_id TEXT,
    replacement_public_key TEXT,
    through_seq INTEGER NOT NULL DEFAULT 0 CHECK(through_seq>=0),
    checkpoint INTEGER NOT NULL DEFAULT 0 CHECK(checkpoint>=0 AND checkpoint<=through_seq),
    key_verified INTEGER NOT NULL DEFAULT 0 CHECK(key_verified IN (0,1))
);
CREATE TABLE IF NOT EXISTS label_signing_selection (
    id INTEGER PRIMARY KEY CHECK(id=1),
    stream_id TEXT NOT NULL,
    issuer TEXT NOT NULL,
    key_id TEXT NOT NULL,
    public_key TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS label_hosts (
    host_id TEXT PRIMARY KEY,
    started_ms INTEGER NOT NULL,
    last_seen_ms INTEGER NOT NULL,
    stopped INTEGER NOT NULL DEFAULT 0 CHECK(stopped IN (0,1))
);
CREATE TABLE IF NOT EXISTS label_host_roles (
    host_id TEXT PRIMARY KEY,
    requires_signing_key INTEGER NOT NULL CHECK(requires_signing_key IN (0,1))
);
CREATE TABLE IF NOT EXISTS label_maintenance_hosts (
    operation_id TEXT NOT NULL,
    host_id TEXT NOT NULL,
    acknowledged INTEGER NOT NULL DEFAULT 0 CHECK(acknowledged IN (0,1)),
    key_activated INTEGER NOT NULL DEFAULT 0 CHECK(key_activated IN (0,1)),
    PRIMARY KEY(operation_id, host_id)
);
INSERT OR IGNORE INTO label_maintenance(id, stream_id) VALUES(1, '00000000000000000000000000000000');
INSERT OR IGNORE INTO label_schema(id, version) VALUES(1, 8);
