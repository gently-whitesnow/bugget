-- Personal access tokens: неинтерактивный доступ к API вместо OIDC-сессии (скрипты, MCP).
--
-- Секрет не хранится: в token_hash лежит SHA-256 полного значения токена. Хэш быстрый и
-- без соли намеренно — секрет генерируется из 32 случайных байт, перебирать там нечего,
-- а проверять хэш нужно на каждом запросе. Ни одна функция ниже token_hash не возвращает,
-- поэтому в приложение он не попадает даже случайно.
--
-- Токен привязан к одной паре workspace+team: схема аутентификации сверит её с контекстом
-- запроса. Отзыв — revoked_at, а не DELETE: запись нужна для истории и для того, чтобы
-- отозванный секрет нельзя было выпустить повторно.
CREATE TABLE IF NOT EXISTS personal_access_tokens(
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id bigint NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    workspace_id int NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    team_id int NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    label varchar(128) NOT NULL,
    token_hash bytea NOT NULL UNIQUE,
    token_prefix varchar(32) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz,
    last_used_at timestamptz,
    revoked_at timestamptz
);

-- Аутентификация ищет строго по хэшу — это самый горячий путь.
CREATE UNIQUE INDEX IF NOT EXISTS ux_personal_access_tokens_token_hash
    ON personal_access_tokens(token_hash);

-- Список токенов в настройках: свои, свежие сверху.
CREATE INDEX IF NOT EXISTS ix_personal_access_tokens_user_id
    ON personal_access_tokens(user_id, created_at DESC);

CREATE OR REPLACE FUNCTION create_personal_access_token(
    p_user_id bigint,
    p_workspace_id int,
    p_team_id int,
    p_label text,
    p_token_hash bytea,
    p_token_prefix text,
    p_expires_at timestamptz)
    RETURNS TABLE(
        id bigint,
        user_id bigint,
        workspace_id int,
        team_id int,
        label varchar(128),
        token_prefix varchar(32),
        created_at timestamptz,
        expires_at timestamptz,
        last_used_at timestamptz,
        revoked_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY INSERT INTO personal_access_tokens AS pat(
        user_id, workspace_id, team_id, label, token_hash, token_prefix, expires_at)
        VALUES(p_user_id, p_workspace_id, p_team_id, p_label, p_token_hash, p_token_prefix, p_expires_at)
    RETURNING
        pat.id,
        pat.user_id,
        pat.workspace_id,
        pat.team_id,
        pat.label,
        pat.token_prefix,
        pat.created_at,
        pat.expires_at,
        pat.last_used_at,
        pat.revoked_at;
END;
$$;

-- Список токенов пользователя по всем его командам: токены — его собственные данные, а
-- прятать их за текущим workspace/team означало бы «токен пропал» при переключении команды.
-- Область каждого токена видна в самой строке.
CREATE OR REPLACE FUNCTION list_personal_access_tokens(p_user_id bigint)
    RETURNS TABLE(
        id bigint,
        user_id bigint,
        workspace_id int,
        team_id int,
        label varchar(128),
        token_prefix varchar(32),
        created_at timestamptz,
        expires_at timestamptz,
        last_used_at timestamptz,
        revoked_at timestamptz)
    LANGUAGE sql
    AS $$
    SELECT
        pat.id,
        pat.user_id,
        pat.workspace_id,
        pat.team_id,
        pat.label,
        pat.token_prefix,
        pat.created_at,
        pat.expires_at,
        pat.last_used_at,
        pat.revoked_at
    FROM personal_access_tokens pat
    WHERE pat.user_id = p_user_id AND pat.revoked_at IS NULL
    ORDER BY pat.created_at DESC;
$$;

-- Поиск для аутентификации. Просроченный и отозванный тоже возвращаются: решение о
-- пригодности принимает домен, иначе «токен не найден» и «токен истёк» неразличимы.
CREATE OR REPLACE FUNCTION find_personal_access_token_by_hash(p_token_hash bytea)
    RETURNS TABLE(
        id bigint,
        user_id bigint,
        workspace_id int,
        team_id int,
        label varchar(128),
        token_prefix varchar(32),
        created_at timestamptz,
        expires_at timestamptz,
        last_used_at timestamptz,
        revoked_at timestamptz)
    LANGUAGE sql
    AS $$
    SELECT
        pat.id,
        pat.user_id,
        pat.workspace_id,
        pat.team_id,
        pat.label,
        pat.token_prefix,
        pat.created_at,
        pat.expires_at,
        pat.last_used_at,
        pat.revoked_at
    FROM personal_access_tokens pat
    WHERE pat.token_hash = p_token_hash
    LIMIT 1;
$$;

-- Отзыв. p_user_id в условии — чтобы нельзя было отозвать чужой токен, зная его id.
-- Повторный отзыв не трогает revoked_at и возвращает false: время первого отзыва — факт.
CREATE OR REPLACE FUNCTION revoke_personal_access_token(p_id bigint, p_user_id bigint)
    RETURNS boolean
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_revoked int;
BEGIN
    UPDATE personal_access_tokens
    SET revoked_at = now()
    WHERE id = p_id AND user_id = p_user_id AND revoked_at IS NULL;

    GET DIAGNOSTICS v_revoked = ROW_COUNT;
    RETURN v_revoked > 0;
END;
$$;

-- Отметка об использовании. Отдельная функция: пишется вне транзакции запроса, промах
-- по ней не должен ломать сам запрос.
CREATE OR REPLACE FUNCTION touch_personal_access_token_last_used(p_id bigint)
    RETURNS void
    LANGUAGE sql
    AS $$
    UPDATE personal_access_tokens
    SET last_used_at = now()
    WHERE id = p_id;
$$;
