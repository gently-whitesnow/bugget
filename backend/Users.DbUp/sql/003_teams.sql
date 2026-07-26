CREATE OR REPLACE FUNCTION create_team(p_workspace_id int, p_name text)
    RETURNS SETOF teams
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_team_id int;
BEGIN
    RETURN QUERY
    INSERT INTO teams(workspace_id, name)
        VALUES (p_workspace_id, p_name)
    RETURNING *;
END;
$$;

CREATE OR REPLACE FUNCTION list_teams(p_workspace_ids int[])
    RETURNS SETOF teams
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT * FROM teams WHERE workspace_id = ANY(p_workspace_ids);
END;
$$;