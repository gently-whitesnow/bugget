CREATE OR REPLACE FUNCTION create_workspace_member(p_user_id bigint, p_workspace_id int, p_role varchar(32))
  RETURNS SETOF workspaces_members
  LANGUAGE plpgsql
  AS $$
BEGIN
  RETURN QUERY INSERT INTO workspaces_members(user_id, workspace_id, role)
    VALUES(p_user_id, p_workspace_id, p_role)
  ON CONFLICT(workspace_id, user_id)
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

CREATE OR REPLACE FUNCTION update_workspace_member(p_user_id bigint, p_workspace_id int, p_role varchar(32))
  RETURNS SETOF workspaces_members
  LANGUAGE plpgsql
  AS $$
BEGIN
  RETURN QUERY UPDATE
    workspaces_members
  SET
    ROLE = p_role
  WHERE
    user_id = p_user_id
    AND workspace_id = p_workspace_id
  RETURNING
    *;
END;
$$;

CREATE OR REPLACE FUNCTION create_team_member(p_user_id bigint, p_team_id int)
  RETURNS SETOF teams_members
  LANGUAGE plpgsql
  AS $$
BEGIN
  RETURN QUERY INSERT INTO teams_members(user_id, team_id)
    VALUES(p_user_id, p_team_id)
  ON CONFLICT(team_id, user_id)
    DO NOTHING
  RETURNING
    *;
  -- если вставка не произошла — возвращаем уже существующую запись
  IF NOT FOUND THEN
    RETURN QUERY
    SELECT
      *
    FROM
      teams_members
    WHERE
      user_id = p_user_id
      AND team_id = p_team_id;
  END IF;
END;
$$;

CREATE OR REPLACE FUNCTION list_teams_member(p_user_id bigint)
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