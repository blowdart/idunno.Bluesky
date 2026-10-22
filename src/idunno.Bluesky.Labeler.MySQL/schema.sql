CREATE TABLE IF NOT EXISTS label_schema (id INTEGER PRIMARY KEY CHECK(id=1), version INTEGER NOT NULL CHECK(version>0)) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_state (
    id INTEGER PRIMARY KEY CHECK(id=1),
    stream_id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    issuer TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    revision BIGINT NOT NULL DEFAULT 0 CHECK(revision>=0),
    issued BIGINT NOT NULL DEFAULT 0 CHECK(issued>=0),
    published BIGINT NOT NULL DEFAULT 0,
    replay_floor BIGINT NOT NULL DEFAULT 0,
    phase INTEGER NOT NULL DEFAULT 0 CHECK(phase BETWEEN 0 AND 6),
    retirement_upper BIGINT NOT NULL DEFAULT 0,
    retirement_through BIGINT NOT NULL DEFAULT 0,
    grace_deadline BIGINT,
    CHECK(published>=0 AND published<=issued),
    CHECK(replay_floor>=0 AND replay_floor<=published),
    CHECK(retirement_upper>=0 AND retirement_upper<=issued),
    CHECK(retirement_through>=0 AND retirement_through<=retirement_upper)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_entries (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    signed_json LONGBLOB NOT NULL,
    payload LONGBLOB NOT NULL,
    format_version INTEGER NOT NULL CHECK(format_version=1),
    queued_at BIGINT NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_operations (
    operation_hash BINARY(32) PRIMARY KEY,
    operation_id TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    first_seq BIGINT NOT NULL CHECK(first_seq>=0),
    last_seq BIGINT NOT NULL,
    revision BIGINT NOT NULL CHECK(revision>0),
    request LONGBLOB NOT NULL,
    INDEX label_operations_revision(revision),
    CHECK(last_seq>=first_seq)
) ENGINE=InnoDB;
INSERT INTO label_state(id, stream_id, issuer) VALUES(1, '00000000000000000000000000000000', '') ON DUPLICATE KEY UPDATE id=id;
INSERT INTO label_schema(id, version) VALUES(1, 1) ON DUPLICATE KEY UPDATE id=id;
