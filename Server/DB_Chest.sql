CREATE TABLE IF NOT EXISTS chest_games (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    identifier VARCHAR(255) NOT NULL,
    bet_amount_k BIGINT NOT NULL,
    selected_item_ids TEXT,
    won BOOLEAN NULL,
    prize_value_k BIGINT NULL,
    status INT NOT NULL,
    message_id BIGINT UNSIGNED NULL,
    channel_id BIGINT UNSIGNED NULL,
    created_at DATETIME NOT NULL,
    updated_at DATETIME NOT NULL
);
