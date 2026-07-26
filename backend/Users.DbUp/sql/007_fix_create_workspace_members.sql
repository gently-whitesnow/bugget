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
            DO NOTHING
        RETURNING
            *;
            
        -- если вставка не произошла — возвращаем уже существующую запись
        IF NOT FOUND THEN
            RETURN QUERY
            SELECT
                *
            FROM
                workspaces_members wm
            WHERE
                wm.user_id = p_user_id
                AND wm.workspace_id = p_workspace_id;
        END IF;
END;
$$;