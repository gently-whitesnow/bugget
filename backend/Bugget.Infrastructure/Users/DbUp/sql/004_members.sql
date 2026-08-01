-- 1) Список участий пользователя в воркспейсах
CREATE OR REPLACE FUNCTION list_workspaces_members(p_user_id bigint)
    RETURNS SETOF workspaces_members
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        workspaces_members
    WHERE
        user_id = p_user_id;
END;
$$;

-- 2) Список участий пользователя в командах
CREATE OR REPLACE FUNCTION list_teams_members(p_user_id bigint)
    RETURNS SETOF teams_members
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        teams_members
    WHERE
        user_id = p_user_id;
END;
$$;

-- 3) Создание участника воркспейса
CREATE OR REPLACE FUNCTION create_workspace_member(p_user_id bigint, p_workspace_id int, p_role varchar(32), p_size_limit int)
    RETURNS SETOF workspaces_members
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_members_count int;
BEGIN
    PERFORM
        pg_advisory_xact_lock(1, p_workspace_id);
    -- 1) Количество участников
    SELECT
        COUNT(*) INTO v_members_count
    FROM
        workspaces_members wm
    WHERE
        wm.workspace_id = p_workspace_id;
    IF v_members_count >= p_size_limit THEN
        RAISE EXCEPTION 'workspace_limit_exceeded'
            USING ERRCODE = 'P0001';
        END IF;
        RETURN QUERY INSERT INTO workspaces_members(user_id, workspace_id, role)
            VALUES (p_user_id, p_workspace_id, p_role)
        ON CONFLICT (workspace_id, user_id)
            DO UPDATE
        SET
            ROLE = p_role
        RETURNING
            *;
END;
$$;

-- 4) Создание участника команды
CREATE OR REPLACE FUNCTION create_team_member(
    p_user_id bigint,
    p_team_id int,
    p_size_limit int
)
RETURNS SETOF teams_members
LANGUAGE plpgsql
AS $$
DECLARE
    v_members_count int;
BEGIN
    PERFORM pg_advisory_xact_lock(1, p_team_id);

    -- Проверяем лимит
    SELECT COUNT(*) INTO v_members_count
    FROM teams_members tm
    WHERE tm.team_id = p_team_id;

    IF v_members_count >= p_size_limit THEN
        RAISE EXCEPTION 'team_limit_exceeded' USING ERRCODE = 'P0001';
    END IF;

    -- Пытаемся вставить
    BEGIN
        RETURN QUERY
        INSERT INTO teams_members(user_id, team_id)
        VALUES (p_user_id, p_team_id)
        ON CONFLICT (team_id, user_id)
            DO NOTHING
        RETURNING *;

        -- если вставка не произошла — возвращаем уже существующую запись
        IF NOT FOUND THEN
            RETURN QUERY
            SELECT * FROM teams_members
            WHERE user_id = p_user_id AND team_id = p_team_id;
        END IF;
    END;

END;
$$;

CREATE OR REPLACE FUNCTION list_team_members(p_team_id int)
    RETURNS SETOF teams_members
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        teams_members
    WHERE
        team_id = p_team_id;
END;
$$;

CREATE OR REPLACE FUNCTION list_workspace_members(p_workspace_id int)
    RETURNS SETOF workspaces_members
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        workspaces_members
    WHERE
        workspace_id = p_workspace_id;
END;
$$;

