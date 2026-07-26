-- Таблица внешних привязок пользователей (multi-provider auth)
CREATE TABLE IF NOT EXISTS user_external_links(
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id bigint NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    provider varchar(32) NOT NULL,
    external_id varchar(256) NOT NULL,
    email varchar(256),
    linked_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(provider, external_id)
);

CREATE INDEX IF NOT EXISTS idx_user_external_links_user_id ON user_external_links(user_id);

-- Миграция существующих пользователей: ExternalId → UserExternalLinks с provider = 'telegram'
INSERT INTO user_external_links(user_id, provider, external_id, linked_at)
SELECT id, 'telegram', external_id, registration_date
FROM users
WHERE external_id IS NOT NULL AND external_id != ''
ON CONFLICT (provider, external_id) DO NOTHING;

-- Поиск пользователя по провайдеру и внешнему ID
CREATE OR REPLACE FUNCTION find_user_by_provider(p_provider text, p_external_id text)
    RETURNS TABLE(user_id bigint)
    LANGUAGE sql
    AS $$
    SELECT uel.user_id
    FROM user_external_links uel
    WHERE uel.provider = p_provider AND uel.external_id = p_external_id
    LIMIT 1;
$$;

-- Добавить привязку
CREATE OR REPLACE FUNCTION add_external_link(p_user_id bigint, p_provider text, p_external_id text, p_email text)
    RETURNS TABLE(
        id bigint,
        user_id bigint,
        provider varchar(32),
        external_id varchar(256),
        email varchar(256),
        linked_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY INSERT INTO user_external_links(user_id, provider, external_id, email)
        VALUES(p_user_id, p_provider, p_external_id, p_email)
    RETURNING
        user_external_links.id,
        user_external_links.user_id,
        user_external_links.provider,
        user_external_links.external_id,
        user_external_links.email,
        user_external_links.linked_at;
END;
$$;

-- Удалить привязку по провайдеру
CREATE OR REPLACE FUNCTION remove_external_link(p_user_id bigint, p_provider text)
    RETURNS void
    LANGUAGE sql
    AS $$
    DELETE FROM user_external_links
    WHERE user_id = p_user_id AND provider = p_provider;
$$;

-- Список привязок пользователя
CREATE OR REPLACE FUNCTION get_external_links(p_user_id bigint)
    RETURNS TABLE(
        id bigint,
        user_id bigint,
        provider varchar(32),
        external_id varchar(256),
        email varchar(256),
        linked_at timestamptz)
    LANGUAGE sql
    AS $$
    SELECT
        uel.id,
        uel.user_id,
        uel.provider,
        uel.external_id,
        uel.email,
        uel.linked_at
    FROM user_external_links uel
    WHERE uel.user_id = p_user_id
    ORDER BY uel.linked_at;
$$;

-- Обновляем get_user_by_external_id: теперь ищем через user_external_links
CREATE OR REPLACE FUNCTION get_user_by_external_id(p_external_id text)
    RETURNS SETOF users
    LANGUAGE sql
    AS $$
    SELECT u.*
    FROM users u
    JOIN user_external_links uel ON uel.user_id = u.id
    WHERE uel.external_id = p_external_id
    LIMIT 1;
$$;
