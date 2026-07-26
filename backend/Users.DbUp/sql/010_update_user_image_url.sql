CREATE OR REPLACE FUNCTION update_user_image_url(p_user_id bigint, p_image_url text)
RETURNS void
LANGUAGE sql
AS $$
    UPDATE users
    SET image_url = p_image_url,
        updated_at = now()
    WHERE id = p_user_id;
$$;
