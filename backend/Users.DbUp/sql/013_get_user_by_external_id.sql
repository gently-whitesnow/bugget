CREATE OR REPLACE FUNCTION get_user_by_external_id(p_external_id text)
    RETURNS SETOF users
    LANGUAGE sql
    AS $$
    SELECT
        *
    FROM
        users u
    WHERE
        u.external_id = p_external_id
    LIMIT 1
$$;
