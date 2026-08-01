CREATE OR REPLACE FUNCTION update_team(p_workspace_id int, p_team_id int, p_name text)
    RETURNS SETOF teams
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    UPDATE teams
    SET name = p_name,
        updated_at = now()
    WHERE id = p_team_id
      AND workspace_id = p_workspace_id
    RETURNING *;
END;
$$;

CREATE OR REPLACE FUNCTION delete_team(p_workspace_id int, p_team_id int)
    RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
    DELETE FROM teams
    WHERE id = p_team_id
      AND workspace_id = p_workspace_id;
END;
$$;
