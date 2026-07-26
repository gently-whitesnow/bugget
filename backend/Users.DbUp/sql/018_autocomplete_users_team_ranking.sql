CREATE OR REPLACE FUNCTION autocomplete_users(
    p_workspace_id   int,
    p_search_string  text,
    p_skip           int,
    p_take           int,
    p_team_id        int DEFAULT NULL
)
RETURNS SETOF public.users
LANGUAGE sql
AS $$
    SELECT u.*
    FROM public.users AS u
    JOIN public.workspaces_members AS wm ON wm.user_id = u.id
    LEFT JOIN public.teams_members AS tm
        ON tm.user_id = u.id AND tm.team_id = p_team_id
    WHERE wm.workspace_id = p_workspace_id
      AND (p_search_string IS NULL OR p_search_string = '' OR u.name ILIKE '%' || p_search_string || '%')
    ORDER BY
        CASE WHEN tm.user_id IS NOT NULL THEN 0 ELSE 1 END,
        u.name
    LIMIT p_take OFFSET p_skip
$$;
