CREATE OR REPLACE FUNCTION update_workspace(p_workspace_id int, p_name text)
    RETURNS SETOF workspaces
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    UPDATE workspaces
    SET name = p_name,
        updated_at = now()
    WHERE id = p_workspace_id
    RETURNING *;
END;
$$;
