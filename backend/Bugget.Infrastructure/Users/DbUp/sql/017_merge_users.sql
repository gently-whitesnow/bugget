-- Проверка: является ли пользователь администратором (владельцем) хотя бы одного воркспейса
CREATE OR REPLACE FUNCTION check_user_owns_workspaces(p_user_id bigint)
    RETURNS boolean
    LANGUAGE sql
    AS $$
    SELECT EXISTS(
        SELECT 1
        FROM workspaces_members
        WHERE user_id = p_user_id AND role = 'admin'
    );
$$;

-- Мёрж пользователей: перенос данных source → target, удаление source
CREATE OR REPLACE FUNCTION merge_users(p_target_user_id bigint, p_source_user_id bigint)
    RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
    -- Перенос external_links (только для провайдеров, которых нет у target)
    UPDATE user_external_links
    SET user_id = p_target_user_id
    WHERE user_id = p_source_user_id
      AND provider NOT IN (
          SELECT provider FROM user_external_links WHERE user_id = p_target_user_id
      );

    -- Перенос MattermostUserId если у target нет
    UPDATE users
    SET mattermost_user_id = (SELECT mattermost_user_id FROM users WHERE id = p_source_user_id)
    WHERE id = p_target_user_id
      AND mattermost_user_id IS NULL
      AND (SELECT mattermost_user_id FROM users WHERE id = p_source_user_id) IS NOT NULL;

    -- Удаление source (каскадно удалит оставшиеся external_links и workspace_members)
    DELETE FROM users WHERE id = p_source_user_id;
END;
$$;
