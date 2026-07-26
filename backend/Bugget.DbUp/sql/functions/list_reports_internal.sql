-- Возвращает проекцию набора заявок по массиву ID. Используется в bulk-read
-- сценариях (см. ReportsDbClient.GetReportsAsync). Колонка `public_id` тут
-- сознательно опущена — её требует только одиночный get_report_internal.

CREATE OR REPLACE FUNCTION public.list_reports_internal(_report_ids int[])
    RETURNS TABLE(
        id int,
        team_report_id int,
        title text,
        status text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_user_id text,
        creator_team_id text,
        responsible_user_id text,
        past_responsible_user_id text,
        creator_type smallint,
        is_excluded_from_analytics boolean)
    LANGUAGE sql
    STABLE
    AS $$
    SELECT
        r.id,
        r.team_report_id,
        r.title,
        r.status,
        r.created_at,
        r.updated_at,
        r.creator_user_id,
        r.creator_team_id,
        r.responsible_user_id,
        r.past_responsible_user_id,
        r.creator_type,
        r.is_excluded_from_analytics
    FROM
        public.reports r
    WHERE
        r.id = ANY(_report_ids)
$$;
