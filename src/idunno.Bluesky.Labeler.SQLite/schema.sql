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
INSERT OR IGNORE INTO label_schema(id, version) VALUES(1, 2);
