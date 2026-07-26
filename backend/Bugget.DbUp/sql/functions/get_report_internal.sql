-- Возвращает полную проекцию одной заявки по её ID. Используется в read-path
-- bugget-api (см. ReportsDbClient.GetReportAsync и др.). Дополнительные
-- проекции (только status / responsible_user_id) переиспользуют ту же функцию
-- — за счёт STABLE результат кэшируется в пределах транзакции.

CREATE OR REPLACE FUNCTION public.get_report_internal(_report_id int)
    RETURNS TABLE(
        id int,
        team_report_id int,
        public_id uuid,
        title text,
        status int,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_user_id text,
        creator_team_id text,
        responsible_user_id text,
        past_responsible_user_id text,
        creator_type smallint,
        is_excluded_from_analytics boolean)
    LANGUAGE plpgsql
    STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        r.id,
        r.team_report_id,
        r.public_id,
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
        r.id = _report_id;
END;
$$;
