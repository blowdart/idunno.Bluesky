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
CREATE TABLE IF NOT EXISTS label_decision_index (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    identity_hash BYTEA NOT NULL CHECK(octet_length(identity_hash)=32),
    identity_key BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_decision_index_identity ON label_decision_index(identity_hash, seq);
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
CREATE INDEX IF NOT EXISTS label_admin_token_expiry ON label_admin_tokens(expires_ms);
CREATE TABLE IF NOT EXISTS label_maintenance (
    id INTEGER PRIMARY KEY CHECK(id=1),
    operation_id TEXT COLLATE "C",
    phase INTEGER NOT NULL DEFAULT 0 CHECK(phase BETWEEN 0 AND 5),
    stream_id VARCHAR(32) COLLATE "C" NOT NULL,
    previous_key_id TEXT COLLATE "C",
    replacement_key_id TEXT COLLATE "C",
    replacement_public_key TEXT COLLATE "C",
    through_seq BIGINT NOT NULL DEFAULT 0 CHECK(through_seq>=0),
    checkpoint BIGINT NOT NULL DEFAULT 0 CHECK(checkpoint>=0 AND checkpoint<=through_seq),
    key_verified INTEGER NOT NULL DEFAULT 0 CHECK(key_verified IN (0,1))
);
CREATE TABLE IF NOT EXISTS label_signing_selection (
    id INTEGER PRIMARY KEY CHECK(id=1),
    stream_id VARCHAR(32) COLLATE "C" NOT NULL,
    issuer TEXT COLLATE "C" NOT NULL,
    key_id VARCHAR(128) COLLATE "C" NOT NULL,
    public_key VARCHAR(512) COLLATE "C" NOT NULL
);
CREATE TABLE IF NOT EXISTS label_hosts (
    host_id VARCHAR(32) COLLATE "C" PRIMARY KEY,
    started_ms BIGINT NOT NULL,
    last_seen_ms BIGINT NOT NULL,
    stopped INTEGER NOT NULL DEFAULT 0 CHECK(stopped IN (0,1))
);
CREATE TABLE IF NOT EXISTS label_host_roles (
    host_id VARCHAR(32) COLLATE "C" PRIMARY KEY,
    requires_signing_key INTEGER NOT NULL CHECK(requires_signing_key IN (0,1))
);
CREATE TABLE IF NOT EXISTS label_maintenance_hosts (
    operation_id TEXT COLLATE "C" NOT NULL,
    host_id VARCHAR(32) COLLATE "C" NOT NULL,
    acknowledged INTEGER NOT NULL DEFAULT 0 CHECK(acknowledged IN (0,1)),
    key_activated INTEGER NOT NULL DEFAULT 0 CHECK(key_activated IN (0,1)),
    PRIMARY KEY(operation_id, host_id)
);
INSERT INTO label_maintenance(id, stream_id) VALUES(1, '00000000000000000000000000000000') ON CONFLICT DO NOTHING;
CREATE TABLE IF NOT EXISTS label_cases (
    case_id BIGINT PRIMARY KEY CHECK(case_id>0),
    subject_hash BYTEA NOT NULL CHECK(octet_length(subject_hash)=32),
    subject_key BYTEA NOT NULL,
    status INTEGER NOT NULL CHECK(status BETWEEN 0 AND 2),
    assignee_hash BYTEA NULL CHECK(assignee_hash IS NULL OR octet_length(assignee_hash)=32),
    assignee_key BYTEA NULL,
    payload BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_cases_subject ON label_cases(subject_hash, status);
CREATE INDEX IF NOT EXISTS label_cases_status ON label_cases(status, case_id);
CREATE INDEX IF NOT EXISTS label_cases_assignee ON label_cases(assignee_hash, status, case_id);
CREATE TABLE IF NOT EXISTS label_case_events (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    case_id BIGINT NOT NULL REFERENCES label_cases(case_id),
    operation_hash BYTEA NOT NULL UNIQUE CHECK(octet_length(operation_hash)=32),
    operation_id TEXT COLLATE "C" NOT NULL,
    intent BYTEA NOT NULL,
    payload BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_case_events_history ON label_case_events(case_id, seq);
CREATE TABLE IF NOT EXISTS label_reports (
    report_id BIGINT PRIMARY KEY CHECK(report_id>0),
    case_id BIGINT NOT NULL REFERENCES label_cases(case_id),
    reason_hash BYTEA NOT NULL CHECK(octet_length(reason_hash)=32),
    reason_type TEXT COLLATE "C" NOT NULL,
    payload BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_reports_case ON label_reports(case_id, report_id);
CREATE INDEX IF NOT EXISTS label_reports_reason ON label_reports(reason_hash, case_id);
CREATE TABLE IF NOT EXISTS label_pending_reports (
    report_id BIGINT PRIMARY KEY CHECK(report_id>0),
    pending BOOLEAN NOT NULL,
    payload BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_pending_reports_status ON label_pending_reports(pending, report_id);
CREATE TABLE IF NOT EXISTS label_report_tokens (
    replay_hash BYTEA PRIMARY KEY CHECK(octet_length(replay_hash)=32),
    replay_key BYTEA NOT NULL,
    token_hash BYTEA NOT NULL UNIQUE CHECK(octet_length(token_hash)=32),
    expires_ms BIGINT NOT NULL
);
CREATE INDEX IF NOT EXISTS label_report_tokens_expiry ON label_report_tokens(expires_ms);
CREATE TABLE IF NOT EXISTS label_policy_gate (id INTEGER PRIMARY KEY CHECK(id=1), payload BYTEA NOT NULL);
CREATE TABLE IF NOT EXISTS label_policy_history (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    operation_hash BYTEA NULL UNIQUE CHECK(operation_hash IS NULL OR octet_length(operation_hash)=32),
    payload BYTEA NOT NULL
);
CREATE TABLE IF NOT EXISTS label_policy_versions (
    version_hash BYTEA PRIMARY KEY CHECK(octet_length(version_hash)=32),
    version TEXT COLLATE "C" NOT NULL,
    payload BYTEA NOT NULL
);
CREATE TABLE IF NOT EXISTS label_policy_events (
    operation_hash BYTEA PRIMARY KEY CHECK(octet_length(operation_hash)=32),
    operation_id TEXT COLLATE "C" NOT NULL,
    intent BYTEA NOT NULL,
    payload BYTEA NOT NULL
);
CREATE TABLE IF NOT EXISTS label_appeals (
    appeal_id BIGINT PRIMARY KEY CHECK(appeal_id>0),
    case_id BIGINT NOT NULL REFERENCES label_cases(case_id),
    status INTEGER NOT NULL,
    pending_hash BYTEA NOT NULL CHECK(octet_length(pending_hash)=32),
    pending_key BYTEA NOT NULL,
    payload BYTEA NOT NULL
);
CREATE INDEX IF NOT EXISTS label_appeals_case ON label_appeals(case_id, status, appeal_id);
CREATE INDEX IF NOT EXISTS label_appeals_pending ON label_appeals(pending_hash, status);
CREATE TABLE IF NOT EXISTS label_appeal_reports (
    report_id BIGINT PRIMARY KEY REFERENCES label_reports(report_id),
    appeal_id BIGINT NOT NULL REFERENCES label_appeals(appeal_id)
);
CREATE INDEX IF NOT EXISTS label_appeal_reports_appeal ON label_appeal_reports(appeal_id, report_id);
CREATE TABLE IF NOT EXISTS label_subject_index (
    seq BIGINT PRIMARY KEY CHECK(seq>0),
    subject_hash BYTEA NOT NULL CHECK(octet_length(subject_hash)=32),
    subject_key BYTEA NOT NULL,
    value TEXT COLLATE "C" NOT NULL
);
CREATE INDEX IF NOT EXISTS label_subject_index_subject ON label_subject_index(subject_hash, seq);
CREATE TABLE IF NOT EXISTS label_case_decisions (
    operation_hash BYTEA PRIMARY KEY CHECK(octet_length(operation_hash)=32),
    operation_id TEXT COLLATE "C" NOT NULL,
    case_id BIGINT NOT NULL REFERENCES label_cases(case_id),
    intent BYTEA NOT NULL,
    payload BYTEA NOT NULL
);
CREATE TABLE IF NOT EXISTS label_search_handles (
    subject_hash BYTEA PRIMARY KEY CHECK(octet_length(subject_hash)=32),
    subject_key BYTEA NOT NULL,
    handle TEXT COLLATE "C" NULL,
    checked_ms BIGINT NOT NULL
);
CREATE TABLE IF NOT EXISTS label_search_text (
    kind INTEGER NOT NULL CHECK(kind IN (0,1)),
    source_id BIGINT NOT NULL,
    content TEXT COLLATE "C" NOT NULL,
    PRIMARY KEY(kind,source_id)
);
CREATE TABLE IF NOT EXISTS label_post_review_checks (
    case_id BIGINT PRIMARY KEY REFERENCES label_cases(case_id),
    checked_ms BIGINT NOT NULL
);
INSERT INTO label_schema(id, version) VALUES(1, 11) ON CONFLICT DO NOTHING;
