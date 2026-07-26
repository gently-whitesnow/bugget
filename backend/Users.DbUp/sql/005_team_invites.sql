CREATE OR REPLACE FUNCTION create_team_invite(in_workspace_id int, in_team_id int, in_token_hash bytea, in_expires_at timestamptz)
    RETURNS SETOF team_invites
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_members_count int;
    v_active_invites_count int;
BEGIN
    -- 4) Создаём инвайт
    RETURN QUERY INSERT INTO team_invites(workspace_id, team_id, token_hash, expires_at)
        VALUES (in_workspace_id, in_team_id, in_token_hash, in_expires_at)
    ON CONFLICT (team_id)
        DO UPDATE SET
            token_hash = in_token_hash,
            expires_at = in_expires_at,
            created_at = now()
RETURNING
    *;
END;
$$;

CREATE OR REPLACE FUNCTION delete_team_invite(in_team_id int, in_id int)
    RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
    DELETE FROM team_invites
    WHERE team_id = in_team_id
        AND id = in_id;
END;
$$;

CREATE OR REPLACE FUNCTION update_team_invite(in_team_id int, in_id int, in_token_hash bytea, in_expires_at timestamptz)
    RETURNS SETOF team_invites
    LANGUAGE plpgsql
    AS $$
BEGIN
        RETURN QUERY UPDATE
            team_invites
        SET
            token_hash = in_token_hash,
            expires_at = in_expires_at,
            created_at = now()
        WHERE
            id = in_id
            AND team_id = in_team_id
        RETURNING
            *;
END;
$$;

CREATE OR REPLACE FUNCTION get_team_invite(in_team_id int)
    RETURNS SETOF team_invites
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        team_invites
    WHERE
        team_id = in_team_id;
END;
$$;

CREATE OR REPLACE FUNCTION accept_team_invite(in_token_hash bytea)
    RETURNS SETOF team_invites
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        team_invites
    WHERE
        token_hash = in_token_hash;
END;
$$;

