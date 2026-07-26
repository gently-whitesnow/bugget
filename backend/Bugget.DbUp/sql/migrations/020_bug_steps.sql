-- Таблица шагов бага
CREATE TABLE IF NOT EXISTS public.bug_steps(
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    bug_id int NOT NULL REFERENCES public.bugs(id) ON DELETE CASCADE,
    step_number int NOT NULL,
    text text NOT NULL,
    creator_user_id text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

-- Удаление всех вложений шага бага
CREATE OR REPLACE FUNCTION public.delete_bug_step_attachments_internal(_step_id integer)
    RETURNS TABLE(
        id int,
        attach_type int,
        entity_id int,
        storage_key text,
        storage_kind int,
        creator_user_id text,
        length_bytes bigint,
        file_name text,
        mime_type text,
        has_preview boolean,
        is_gzip_compressed boolean,
        created_at timestamp with time zone)
    LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY DELETE FROM public.attachments a
    WHERE a.entity_id = _step_id
        AND a.attach_type = 3
    RETURNING
        a.id,
        a.attach_type,
        a.entity_id,
        a.storage_key,
        a.storage_kind,
        a.creator_user_id,
        a.length_bytes,
        a.file_name,
        a.mime_type,
        a.has_preview,
        a.is_gzip_compressed,
        a.created_at;
END;
$$;

-- Получение списка шагов для отчетов
CREATE OR REPLACE FUNCTION public.list_bug_steps_internal(_report_ids int[])
    RETURNS TABLE(
        id int,
        bug_id int,
        text text,
        step_number int,
        creator_user_id text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE sql
    STABLE
    AS $$
    SELECT
        s.id,
        s.bug_id,
        s.text,
        s.step_number,
        s.creator_user_id,
        s.created_at,
        s.updated_at
    FROM
        public.bug_steps s
        JOIN public.bugs b ON s.bug_id = b.id
    WHERE
        b.report_id = ANY(_report_ids)
    ORDER BY
        s.bug_id,
        s.step_number;
$$;

CREATE OR REPLACE FUNCTION public.list_attachments_internal(_report_ids int[])
    RETURNS TABLE(
        id int,
        attach_type int,
        created_at timestamp with time zone,
        entity_id int,
        storage_key text,
        storage_kind int,
        creator_user_id text,
        length_bytes bigint,
        file_name text,
        mime_type text,
        has_preview boolean,
        is_gzip_compressed boolean
    )
    AS $$
DECLARE
    _fact_attach_type int = 0;
    _expected_attach_type int = 1;
    _comment_attach_type int = 2;
    _bug_step_attach_type int = 3;
BEGIN
    RETURN QUERY
    -- Вложения, привязанные к багам
    SELECT
        a.id,
        a.attach_type,
        a.created_at,
        a.entity_id,
        a.storage_key,
        a.storage_kind,
        a.creator_user_id,
        a.length_bytes,
        a.file_name,
        a.mime_type,
        a.has_preview,
        a.is_gzip_compressed
    FROM
        public.attachments a
        JOIN public.bugs b ON a.entity_id = b.id
    WHERE (a.attach_type = _fact_attach_type
        OR a.attach_type = _expected_attach_type)
        AND b.report_id = ANY (_report_ids)
    UNION ALL
    -- Вложения, привязанные к комментариям
    SELECT
        a.id,
        a.attach_type,
        a.created_at,
        a.entity_id,
        a.storage_key,
        a.storage_kind,
        a.creator_user_id,
        a.length_bytes,
        a.file_name,
        a.mime_type,
        a.has_preview,
        a.is_gzip_compressed
    FROM
        public.attachments a
        JOIN public.comments c ON a.entity_id = c.id
        JOIN public.bugs b ON c.bug_id = b.id
    WHERE
        a.attach_type = _comment_attach_type
        AND b.report_id = ANY (_report_ids)
    UNION ALL
    -- Вложения, привязанные к шагам багов
    SELECT
        a.id,
        a.attach_type,
        a.created_at,
        a.entity_id,
        a.storage_key,
        a.storage_kind,
        a.creator_user_id,
        a.length_bytes,
        a.file_name,
        a.mime_type,
        a.has_preview,
        a.is_gzip_compressed
    FROM
        public.attachments a
        JOIN public.bug_steps bs ON a.entity_id = bs.id
        JOIN public.bugs b ON bs.bug_id = b.id
    WHERE
        a.attach_type = _bug_step_attach_type
        AND b.report_id = ANY (_report_ids);
END;
$$
LANGUAGE plpgsql
STABLE;
