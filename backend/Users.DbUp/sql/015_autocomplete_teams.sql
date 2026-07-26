CREATE OR REPLACE FUNCTION autocomplete_teams(
    p_workspace_id   int,
    p_search_string  text,
    p_skip           int,
    p_take           int
)
RETURNS SETOF public.teams
LANGUAGE sql
AS $$
    SELECT t.*
    FROM public.teams AS t
    WHERE t.workspace_id = p_workspace_id
      AND (p_search_string IS NULL OR t.name ILIKE '%' || p_search_string || '%')
    ORDER BY t.name
    LIMIT GREATEST(p_take, 0)
    OFFSET GREATEST(p_skip, 0)
$$;
