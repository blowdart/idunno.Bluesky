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
CREATE TABLE IF NOT EXISTS label_distribution (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    slot_hash BINARY(32) NOT NULL,
    slot_key LONGBLOB NOT NULL,
    INDEX label_distribution_slot(slot_hash, seq)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_decision_index (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    identity_hash BINARY(32) NOT NULL,
    identity_key LONGBLOB NOT NULL,
    INDEX label_decision_index_identity(identity_hash, seq)
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
CREATE TABLE IF NOT EXISTS label_decisions (
    operation_hash BINARY(32) PRIMARY KEY,
    operation_id TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    revision BIGINT NOT NULL CHECK(revision>0),
    receipt LONGBLOB NOT NULL,
    INDEX label_decisions_revision(revision)
) ENGINE=InnoDB;
INSERT INTO label_state(id, stream_id, issuer) VALUES(1, '00000000000000000000000000000000', '') ON DUPLICATE KEY UPDATE id=id;
CREATE TABLE IF NOT EXISTS label_retirement (id INTEGER PRIMARY KEY CHECK(id=1), payload LONGBLOB NOT NULL) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_retirement_receipts (operation_hash BINARY(32) PRIMARY KEY, payload LONGBLOB NOT NULL) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_retirement_confirmations (token_hash BINARY(32) PRIMARY KEY, expires_ms BIGINT NOT NULL, payload LONGBLOB NOT NULL, INDEX label_retirement_confirmation_expiry(expires_ms)) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_retirement_slots (seq BIGINT PRIMARY KEY, slot_hash BINARY(32) NOT NULL, slot_key LONGBLOB NOT NULL, INDEX label_retirement_slot(slot_hash)) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_team (id INTEGER PRIMARY KEY CHECK(id=1), payload LONGBLOB NOT NULL) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_admin_audit (seq BIGINT PRIMARY KEY, payload LONGBLOB NOT NULL) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_admin_receipts (
    operation_hash BINARY(32) PRIMARY KEY,
    operation_id TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    intent LONGBLOB NOT NULL,
    payload LONGBLOB NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_admin_tokens (
    replay_hash BINARY(32) PRIMARY KEY,
    token_hash VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    actor TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    expires_ms BIGINT NOT NULL,
    INDEX label_admin_token_hash(token_hash),
    INDEX label_admin_token_expiry(expires_ms)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_maintenance (
    id INTEGER PRIMARY KEY CHECK(id=1),
    operation_id VARCHAR(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin,
    phase INTEGER NOT NULL DEFAULT 0 CHECK(phase BETWEEN 0 AND 5),
    stream_id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    previous_key_id VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin,
    replacement_key_id VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin,
    replacement_public_key VARCHAR(512) CHARACTER SET ascii COLLATE ascii_bin,
    through_seq BIGINT NOT NULL DEFAULT 0 CHECK(through_seq>=0),
    checkpoint BIGINT NOT NULL DEFAULT 0,
    key_verified INTEGER NOT NULL DEFAULT 0 CHECK(key_verified IN (0,1)),
    CHECK(checkpoint>=0 AND checkpoint<=through_seq)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_signing_selection (
    id INTEGER PRIMARY KEY CHECK(id=1),
    stream_id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    issuer TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    key_id VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    public_key VARCHAR(512) CHARACTER SET ascii COLLATE ascii_bin NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_hosts (
    host_id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
    started_ms BIGINT NOT NULL,
    last_seen_ms BIGINT NOT NULL,
    stopped INTEGER NOT NULL DEFAULT 0 CHECK(stopped IN (0,1))
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_host_roles (
    host_id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
    requires_signing_key INTEGER NOT NULL CHECK(requires_signing_key IN (0,1))
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_maintenance_hosts (
    operation_id VARCHAR(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    host_id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    acknowledged INTEGER NOT NULL DEFAULT 0 CHECK(acknowledged IN (0,1)),
    key_activated INTEGER NOT NULL DEFAULT 0 CHECK(key_activated IN (0,1)),
    PRIMARY KEY(operation_id, host_id)
) ENGINE=InnoDB;
INSERT INTO label_maintenance(id, stream_id) VALUES(1, '00000000000000000000000000000000') ON DUPLICATE KEY UPDATE id=id;
CREATE TABLE IF NOT EXISTS label_cases (
    case_id BIGINT PRIMARY KEY CHECK(case_id>0),
    subject_hash BINARY(32) NOT NULL,
    subject_key LONGBLOB NOT NULL,
    status INTEGER NOT NULL CHECK(status BETWEEN 0 AND 2),
    assignee_hash BINARY(32) NULL,
    assignee_key LONGBLOB NULL,
    payload LONGBLOB NOT NULL,
    INDEX label_cases_subject(subject_hash, status),
    INDEX label_cases_status(status, case_id),
    INDEX label_cases_assignee(assignee_hash, status, case_id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_case_events (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    case_id BIGINT NOT NULL,
    operation_hash BINARY(32) NOT NULL UNIQUE,
    operation_id TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    intent LONGBLOB NOT NULL,
    payload LONGBLOB NOT NULL,
    FOREIGN KEY(case_id) REFERENCES label_cases(case_id),
    INDEX label_case_events_history(case_id, seq)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_reports (
    report_id BIGINT PRIMARY KEY CHECK(report_id>0),
    case_id BIGINT NOT NULL,
    reason_hash BINARY(32) NOT NULL,
    reason_type TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    payload LONGBLOB NOT NULL,
    FOREIGN KEY(case_id) REFERENCES label_cases(case_id),
    INDEX label_reports_case(case_id, report_id),
    INDEX label_reports_reason(reason_hash, case_id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_pending_reports (
    report_id BIGINT PRIMARY KEY CHECK(report_id>0),
    pending BOOLEAN NOT NULL,
    payload LONGBLOB NOT NULL,
    INDEX label_pending_reports_status(pending, report_id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_report_tokens (
    replay_hash BINARY(32) PRIMARY KEY,
    replay_key LONGBLOB NOT NULL,
    token_hash BINARY(32) NOT NULL UNIQUE,
    expires_ms BIGINT NOT NULL,
    INDEX label_report_tokens_expiry(expires_ms)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_policy_gate (id INTEGER PRIMARY KEY CHECK(id=1), payload LONGBLOB NOT NULL) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_policy_history (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    operation_hash BINARY(32) NULL UNIQUE,
    payload LONGBLOB NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_policy_versions (
    version_hash BINARY(32) PRIMARY KEY,
    version TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    payload LONGBLOB NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_policy_events (
    operation_hash BINARY(32) PRIMARY KEY,
    operation_id TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    intent LONGBLOB NOT NULL,
    payload LONGBLOB NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_appeals (
    appeal_id BIGINT PRIMARY KEY CHECK(appeal_id>0),
    case_id BIGINT NOT NULL,
    status INTEGER NOT NULL,
    pending_hash BINARY(32) NOT NULL,
    pending_key LONGBLOB NOT NULL,
    payload LONGBLOB NOT NULL,
    FOREIGN KEY(case_id) REFERENCES label_cases(case_id),
    INDEX label_appeals_case(case_id, status, appeal_id),
    INDEX label_appeals_pending(pending_hash, status)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_appeal_reports (
    report_id BIGINT PRIMARY KEY,
    appeal_id BIGINT NOT NULL,
    FOREIGN KEY(report_id) REFERENCES label_reports(report_id),
    FOREIGN KEY(appeal_id) REFERENCES label_appeals(appeal_id),
    INDEX label_appeal_reports_appeal(appeal_id, report_id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_subject_index (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    subject_hash BINARY(32) NOT NULL,
    subject_key LONGBLOB NOT NULL,
    value TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    INDEX label_subject_index_subject(subject_hash, seq)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_case_decisions (
    operation_hash BINARY(32) PRIMARY KEY,
    operation_id TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    case_id BIGINT NOT NULL,
    intent LONGBLOB NOT NULL,
    payload LONGBLOB NOT NULL,
    FOREIGN KEY(case_id) REFERENCES label_cases(case_id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_search_handles (
    subject_hash BINARY(32) PRIMARY KEY,
    subject_key BLOB NOT NULL,
    handle TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NULL,
    checked_ms BIGINT NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_search_text (
    kind INT NOT NULL CHECK(kind IN (0,1)),
    source_id BIGINT NOT NULL,
    content TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    PRIMARY KEY(kind,source_id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS label_post_review_checks (
    case_id BIGINT PRIMARY KEY,
    checked_ms BIGINT NOT NULL,
    FOREIGN KEY(case_id) REFERENCES label_cases(case_id)
) ENGINE=InnoDB;
INSERT INTO label_schema(id, version) VALUES(1, 12) ON DUPLICATE KEY UPDATE id=id;
