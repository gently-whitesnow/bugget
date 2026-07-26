CREATE OR REPLACE FUNCTION try_insert_user(p_external_id text, p_name text, p_image_url text)
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