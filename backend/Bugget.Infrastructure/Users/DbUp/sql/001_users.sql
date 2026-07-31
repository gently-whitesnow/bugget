CREATE OR REPLACE FUNCTION upsert_user(p_external_id text, p_name text, p_image_url text)
    RETURNS TABLE(
        id bigint,
        external_id varchar(256),
        name varchar(256),
        image_url varchar(512),
        registration_date timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY INSERT INTO users AS u(external_id, name, image_url)
        VALUES(p_external_id, p_name, p_image_url)
    ON CONFLICT ON CONSTRAINT users_external_id_key
        DO UPDATE SET
            name = EXCLUDED.name,
            image_url = EXCLUDED.image_url,
            updated_at = now()
        RETURNING
            u.id,
            u.external_id,
            u.name,
            u.image_url,
            u.registration_date,
            u.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION get_user(p_user_id bigint)
    RETURNS SETOF users
    LANGUAGE sql
    AS $$
    SELECT
        *
    FROM
        users u
    WHERE
        u.id = p_user_id
    LIMIT 1
$$;

CREATE OR REPLACE FUNCTION list_users(p_user_ids bigint[], p_workspace_id int = null)
RETURNS SETOF public.users
LANGUAGE sql
AS $$
    SELECT u.*
    FROM public.users AS u
    JOIN public.workspaces_members AS wm ON wm.user_id = u.id
    WHERE (p_workspace_id IS NULL OR wm.workspace_id = p_workspace_id)
      AND u.id = ANY (p_user_ids)
$$;


CREATE OR REPLACE FUNCTION autocomplete_users(
    p_workspace_id   int,
    p_search_string  text,
    p_skip           int,
    p_take           int
)
RETURNS SETOF public.users
LANGUAGE sql
AS $$
    SELECT u.*
    FROM public.users AS u
    JOIN public.workspaces_members AS wm ON wm.user_id = u.id
    WHERE wm.workspace_id = p_workspace_id
      AND (p_search_string IS NULL OR u.name ILIKE '%' || p_search_string || '%')
    ORDER BY u.name
    LIMIT p_take OFFSET p_skip
$$;

CREATE OR REPLACE FUNCTION delete_user(
    p_user_id bigint
)
RETURNS void
LANGUAGE sql
AS $$
    DELETE FROM public.users
    WHERE id = p_user_id;
$$;
