CREATE OR REPLACE FUNCTION create_workspace(p_user_id bigint, p_name text)
    RETURNS SETOF workspaces
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_workspace_id int;
BEGIN
    -- 1. создаём воркспейс и сохраняем id
    INSERT INTO workspaces(name)
        VALUES (p_name)
    RETURNING
        id INTO v_workspace_id;
    -- 2. добавляем владельца
    INSERT INTO workspaces_members(workspace_id, user_id, role)
        VALUES (v_workspace_id, p_user_id, 'admin');
    -- 3. возвращаем созданный воркспейс
    RETURN QUERY
    SELECT
        *
    FROM
        workspaces
    WHERE
        id = v_workspace_id;
END;
$$;

CREATE OR REPLACE FUNCTION list_workspaces(p_user_id bigint)
    RETURNS SETOF workspaces
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        workspaces AS w
    WHERE
        w.id IN(
            SELECT
                wm.workspace_id
            FROM
                workspaces_members AS wm
            WHERE
                wm.user_id = p_user_id);
END;
$$;

