CREATE OR REPLACE FUNCTION public.add_participant_if_not_exist_internal(_report_id integer, _user_id text)
    RETURNS text[] -- возвращаем массив идентификаторов или NULL
    LANGUAGE plpgsql
    AS $$
DECLARE
    inserted_count int;
    participants text[];
BEGIN
    -- пытаемся добавить, при конфликте ничего не делаем
    INSERT INTO public.report_participants(report_id, user_id)
        VALUES (_report_id, _user_id)
    ON CONFLICT (report_id, user_id)
        DO NOTHING;
    GET DIAGNOSTICS inserted_count = ROW_COUNT;
    -- если ничего не вставилось — возвращаем NULL
    IF inserted_count = 0 THEN
        RETURN NULL;
    END IF;
    -- собираем всех участников в массив
    SELECT
        array_agg(user_id) INTO participants
    FROM
        public.report_participants
    WHERE
        report_id = _report_id;
    RETURN participants;
END;
$$;

CREATE OR REPLACE FUNCTION public.change_status_internal(_report_id integer, _new_status integer)
    RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
    UPDATE
        public.reports
    SET
        status = _new_status
    WHERE
        id = _report_id;
END;
$$;
