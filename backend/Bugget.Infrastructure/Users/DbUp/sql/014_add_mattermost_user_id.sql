-- Add mattermost_user_id column to users table
ALTER TABLE users ADD COLUMN IF NOT EXISTS mattermost_user_id varchar(64);

-- Recreate try_insert_user to include mattermost_user_id in RETURNS TABLE
drop function if exists try_insert_user;

CREATE OR REPLACE FUNCTION try_insert_user(p_external_id text, p_name text, p_image_url text)
    RETURNS TABLE(
        id bigint,
        external_id varchar(256),
        name varchar(256),
        image_url varchar(512),
        registration_date timestamptz,
        updated_at timestamptz,
        mattermost_user_id varchar(64))
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
            u.updated_at,
            u.mattermost_user_id;
END;
$$;

-- Recreate put_user to include mattermost_user_id in RETURNS TABLE
drop function if exists put_user;
CREATE OR REPLACE FUNCTION put_user(p_id bigint, p_name text)
RETURNS TABLE(
  id bigint,
  external_id varchar(256),
  name varchar(256),
  image_url varchar(512),
  registration_date timestamptz,
  updated_at timestamptz,
  mattermost_user_id varchar(64)
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
    updated_at,
    mattermost_user_id;
$$;

-- New function: update mattermost_user_id
CREATE OR REPLACE FUNCTION update_mattermost_user_id(p_user_id bigint, p_mattermost_user_id text)
RETURNS void
LANGUAGE sql
AS $$
  UPDATE users
  SET mattermost_user_id = p_mattermost_user_id,
      updated_at = now()
  WHERE id = p_user_id;
$$;
