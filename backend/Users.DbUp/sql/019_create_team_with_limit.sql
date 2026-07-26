CREATE OR REPLACE FUNCTION create_team(p_workspace_id int, p_name text, p_size_limit int)
    RETURNS SETOF teams
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_teams_count int;
BEGIN
    PERFORM pg_advisory_xact_lock(2, p_workspace_id);

    SELECT COUNT(*) INTO v_teams_count
    FROM teams t
    WHERE t.workspace_id = p_workspace_id;

    IF v_teams_count >= p_size_limit THEN
        RAISE EXCEPTION 'teams_count_limit_exceeded' USING ERRCODE = 'P0001';
    END IF;

    RETURN QUERY
    INSERT INTO teams(workspace_id, name)
        VALUES (p_workspace_id, p_name)
    RETURNING *;
END;
$$;
