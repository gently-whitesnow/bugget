CREATE OR REPLACE FUNCTION put_user(p_id bigint, p_name text)
RETURNS TABLE(
  id bigint,
  external_id varchar(256),
  name varchar(256),
  image_url varchar(512),
  registration_date timestamptz,
  updated_at timestamptz
)
LANGUAGE sql
AS $$
  UPDATE users
  SET name = p_name,
      updated_at = now()
  WHERE id = p_id
  RETURNING
    id,
    external_id,
    name,
    image_url,
    registration_date,
    updated_at;
$$;