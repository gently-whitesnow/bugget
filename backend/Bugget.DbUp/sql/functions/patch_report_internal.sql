-- Точечное обновление полей заявки. Каждое опциональное поле — `NULL = не
-- менять, значение = выставить`. `past_responsible_user_id` сдвигается только
-- когда ответственный реально меняется (иначе многократный PATCH одним и тем
-- же id затирал бы исходное «прошлое»). Колонки RETURNING достаточно для BO,
-- чтобы сравнить старое и новое состояние без второго запроса.

CREATE OR REPLACE FUNCTION public.patch_report_internal(
    _report_id int,
    _title text DEFAULT NULL,
    _status integer DEFAULT NULL,
    _responsible_user_id text DEFAULT NULL,
    _is_excluded_from_analytics boolean DEFAULT NULL)
    RETURNS TABLE(
        id integer,
        team_report_id int,
        public_id uuid,
        title text,
        status integer,
        responsible_user_id text,
        past_responsible_user_id text,
        updated_at timestamp with time zone,
        creator_team_id text,
        is_excluded_from_analytics boolean)
    LANGUAGE plpgsql
    AS $$
BEGIN
    UPDATE
        public.reports AS r
    SET
        updated_at = now(),
        past_responsible_user_id = CASE
            WHEN _responsible_user_id IS NOT NULL
                 AND _responsible_user_id IS DISTINCT FROM r.responsible_user_id THEN r.responsible_user_id
            ELSE r.past_responsible_user_id
        END,
        status = COALESCE(_status, r.status),
        title = COALESCE(_title, r.title),
        responsible_user_id = COALESCE(_responsible_user_id, r.responsible_user_id),
        is_excluded_from_analytics = COALESCE(_is_excluded_from_analytics, r.is_excluded_from_analytics)
    WHERE
        r.id = _report_id;
    RETURN QUERY
    SELECT
        r.id,
        r.team_report_id,
        r.public_id,
        r.title,
        r.status,
        r.responsible_user_id,
        r.past_responsible_user_id,
        r.updated_at,
        r.creator_team_id,
        r.is_excluded_from_analytics
    FROM
        public.reports AS r
    WHERE
        r.id = _report_id;
END;
$$;
