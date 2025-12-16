--CREATE DATABASE IF NOT EXISTS `prestige`
--  CHARACTER SET utf8mb4
--  COLLATE utf8mb4_unicode_ci;

--USE `prestige`;

--CREATE TABLE `users` (
--  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
--  `identifier` VARCHAR(64) NOT NULL,       -- Discord ID or other unique id
--  `username` VARCHAR(64) NOT NULL,
--  `display_name` VARCHAR(64) NOT NULL,
--  `balance` BIGINT NOT NULL DEFAULT 0,     -- stored in K (thousands)
--  PRIMARY KEY (`id`),
--  UNIQUE KEY `uq_users_identifier` (`identifier`)
--) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

--CREATE TABLE `transactions` (
--  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
--  `user_id` INT UNSIGNED NOT NULL,         -- FK to users.id
--  `identifier` VARCHAR(64) NOT NULL,       -- duplicate of user identifier for convenience
--  `amount_k` BIGINT NOT NULL,              -- amount in thousands (0.5m = 500)
--  `type` TINYINT NOT NULL,                 -- 0 = Deposit, 1 = Withdraw
--  `status` TINYINT NOT NULL,               -- 0 = Pending, 1 = Accepted, 2 = Cancelled, 3 = Denied
--  `staff_id` INT UNSIGNED NULL,            -- optional, if you later store staff as users too
--  `staff_identifier` VARCHAR(64) NULL,     -- Discord ID of staff member
--  `created_at` DATETIME NOT NULL,
--  `updated_at` DATETIME NOT NULL,
--  `notes` TEXT NULL,
--  PRIMARY KEY (`id`),
--  KEY `idx_transactions_user` (`user_id`),
--  CONSTRAINT `fk_transactions_user`
--    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`)
--    ON DELETE CASCADE
--) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

--ALTER TABLE `transactions`
--  ADD COLUMN `user_message_id` BIGINT NULL,
--  ADD COLUMN `user_channel_id` BIGINT NULL,
--  ADD COLUMN `staff_message_id` BIGINT NULL,
--  ADD COLUMN `staff_channel_id` BIGINT NULL;

--CREATE TABLE stakes (
--    id INT AUTO_INCREMENT PRIMARY KEY,
--    user_id INT NOT NULL,
--    identifier VARCHAR(64) NOT NULL,
--    amount_k BIGINT NOT NULL,
--    status INT NOT NULL,
--    user_message_id BIGINT NULL,
--    user_channel_id BIGINT NULL,
--    staff_message_id BIGINT NULL,
--    staff_channel_id BIGINT NULL,
--    created_at DATETIME NOT NULL,
--    updated_at DATETIME NOT NULL
--);

--CREATE TABLE `logs` (
--  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
--  `created_at` DATETIME NOT NULL,
--  `source` VARCHAR(128) NOT NULL,          -- e.g. StakeCommand, TransactionsService
--  `level` VARCHAR(16) NOT NULL,            -- e.g. Info, Error
--  `user_identifier` VARCHAR(64) NULL,      -- Discord/user identifier if relevant
--  `action` VARCHAR(128) NULL,              -- short event key, e.g. CreateStakeException
--  `message` TEXT NULL,                     -- human readable description
--  `exception` TEXT NULL,                   -- full exception.ToString()
--  `metadata_json` TEXT NULL,               -- optional JSON payload
--  PRIMARY KEY (`id`),
--  KEY `idx_logs_created_at` (`created_at`),
--  KEY `idx_logs_source` (`source`),
--  KEY `idx_logs_user_identifier` (`user_identifier`)
--) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

--ALTER TABLE `users`
--  ADD COLUMN `stake_streak` INT NOT NULL DEFAULT 0;

--ALTER TABLE `transactions`
--  ADD COLUMN `fee_k` BIGINT NOT NULL DEFAULT 0 AFTER `amount_k`;

--  ALTER TABLE `stakes`
--  ADD COLUMN `fee_k` BIGINT NOT NULL DEFAULT 0 AFTER `amount_k`;

--ALTER TABLE `users`
--  ADD COLUMN `stake_lose_streak` INT NOT NULL DEFAULT 0 AFTER `stake_streak`;

--CREATE TABLE coinflips (
--    id          INT AUTO_INCREMENT PRIMARY KEY,
--    user_id     INT NOT NULL,
--    identifier  VARCHAR(64) NOT NULL,
--    amount_k    BIGINT NOT NULL,
--    chose_heads TINYINT(1) NULL,
--    result_heads TINYINT(1) NULL,
--    status      INT NOT NULL,
--    message_id  BIGINT NULL,
--    channel_id  BIGINT NULL,
--    created_at  DATETIME NOT NULL,
--    updated_at  DATETIME NOT NULL
--);

--CREATE INDEX idx_coinflips_user_id ON coinflips(user_id);
--CREATE INDEX idx_coinflips_identifier ON coinflips(identifier);

--CREATE TABLE races (
--    Id INT AUTO_INCREMENT PRIMARY KEY,
--    StartTime DATETIME NOT NULL,
--    EndTime DATETIME NOT NULL,
--    Status VARCHAR(32) NOT NULL,
--    PrizeDistributionJson TEXT NOT NULL,
--    ChannelId BIGINT NOT NULL,
--    MessageId BIGINT NOT NULL
--);

--CREATE TABLE race_participants (
--    RaceId INT NOT NULL,
--    UserIdentifier VARCHAR(64) NOT NULL,
--    TotalWagered BIGINT NOT NULL,
--    Username VARCHAR(64) NOT NULL,
--    PRIMARY KEY (RaceId, UserIdentifier),
--    FOREIGN KEY (RaceId) REFERENCES races(Id) ON DELETE CASCADE
--);

--CREATE TABLE balance_adjustments (
--    id               SERIAL PRIMARY KEY,
--    user_id          INT NOT NULL,
--    user_identifier  VARCHAR(64) NOT NULL,  -- mirrors users.identifier
--    staff_id         INT NULL,              -- optional link to staff user
--    staff_identifier VARCHAR(64) NULL,      -- discord id of staff
--    adjustment_type  SMALLINT NOT NULL,     -- 0 = AdminAdd, 1 = AdminGift, 2 = AdminRemove
--    amount_k         BIGINT NOT NULL,       -- always positive
--    source           VARCHAR(64) NOT NULL,  -- e.g. 'AdminCommand', 'System', ...
--    created_at       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
--    reason           TEXT NULL
--);

--CREATE INDEX idx_balance_adjustments_user_created_at
--    ON balance_adjustments (user_id, created_at DESC);

--CREATE INDEX idx_balance_adjustments_staff_created_at
--    ON balance_adjustments (staff_id, created_at DESC);

--CREATE INDEX idx_balance_adjustments_type_created_at
--    ON balance_adjustments (adjustment_type, created_at DESC);

--CREATE TABLE blackjack_games (
--    id INT AUTO_INCREMENT PRIMARY KEY,
--    user_id INT NOT NULL,
--    identifier VARCHAR(64) NOT NULL,
--    bet_amount BIGINT NOT NULL,
--    status INT NOT NULL,
--    deck_state TEXT NOT NULL,
--    dealer_hand TEXT NOT NULL,
--    player_hands TEXT NOT NULL,
--    current_hand_index INT NOT NULL DEFAULT 0,
--    insurance_taken TINYINT UNSIGNED NOT NULL DEFAULT 0,
--    message_id BIGINT NULL,
--    channel_id BIGINT NULL,
--    created_at DATETIME NOT NULL,
--    updated_at DATETIME NOT NULL
--);

--CREATE INDEX idx_blackjack_user_status ON blackjack_games(user_id, status);

--CREATE TABLE higher_lower_games (
--    id INT AUTO_INCREMENT PRIMARY KEY,
--    user_id INT NOT NULL,
--    identifier VARCHAR(64) NOT NULL,
--    bet_amount BIGINT NOT NULL,
--    current_payout DECIMAL(20, 4) NOT NULL,
--    current_round INT NOT NULL DEFAULT 0,
--    max_rounds INT NOT NULL,
--    status INT NOT NULL,
--    last_card TEXT NOT NULL,
--    card_history TEXT NOT NULL,
--    message_id BIGINT NULL,
--    channel_id BIGINT NULL,
--    created_at DATETIME NOT NULL,
--    updated_at DATETIME NOT NULL
--);

--CREATE INDEX idx_hl_games_user_status ON higher_lower_games(user_id, status);


--CREATE TABLE IF NOT EXISTS `mines_games` (
--  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
--  `user_id` INT UNSIGNED NOT NULL,
--  `identifier` VARCHAR(64) NOT NULL,
--  `bet_amount` BIGINT NOT NULL,
--  `mines_count` INT NOT NULL,
--  `status` TINYINT NOT NULL,
--  `mine_locations` TEXT NOT NULL,
--  `revealed_tiles` TEXT NOT NULL,
--  `message_id` BIGINT UNSIGNED NULL,
--  `channel_id` BIGINT UNSIGNED NULL,
--  `created_at` DATETIME NOT NULL,
--  `updated_at` DATETIME NOT NULL,
--  PRIMARY KEY (`id`),
--  KEY `idx_mines_games_user` (`user_id`),
--  CONSTRAINT `fk_mines_games_user`
--    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`)
--    ON DELETE CASCADE
--) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;