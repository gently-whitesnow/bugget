CREATE TABLE IF NOT EXISTS admin_users(
    user_id bigint PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION is_admin_user(p_user_id bigint)
RETURNS boolean
LANGUAGE sql
AS $$
    SELECT EXISTS(
        SELECT 1
        FROM admin_users
        WHERE user_id = p_user_id
    );
$$;

-- Админов заводит владелец инсталляции:
-- INSERT INTO admin_users(user_id) VALUES (<user_id>);
