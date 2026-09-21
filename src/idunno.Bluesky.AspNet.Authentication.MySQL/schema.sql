CREATE TABLE IF NOT EXISTS `idunno_bluesky_identities`
(
    `Did` VARCHAR(2048) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `Identity` LONGBLOB NOT NULL,
    `ExpiresAtUtc` DATETIME(6) NOT NULL,
    PRIMARY KEY (`Did`),
    INDEX `IX_idunno_bluesky_identities_ExpiresAtUtc` (`ExpiresAtUtc`)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS `idunno_bluesky_refresh_locks`
(
    `Did` VARCHAR(2048) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `LockToken` VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `ExpiresAtUtc` DATETIME(6) NOT NULL,
    PRIMARY KEY (`Did`),
    INDEX `IX_idunno_bluesky_refresh_locks_ExpiresAtUtc` (`ExpiresAtUtc`)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS `idunno_bluesky_correlation_states`
(
    `CorrelationId` BINARY(16) NOT NULL,
    `State` LONGTEXT NOT NULL,
    `ExpiresAtUtc` DATETIME(6) NOT NULL,
    PRIMARY KEY (`CorrelationId`),
    INDEX `IX_idunno_bluesky_correlation_states_ExpiresAtUtc` (`ExpiresAtUtc`)
) ENGINE = InnoDB;
