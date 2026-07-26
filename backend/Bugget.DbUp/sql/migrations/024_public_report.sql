DO $$
DECLARE
    pg_major int;
BEGIN
    -- Получаем major-версию PostgreSQL
    pg_major := current_setting('server_version_num')::int / 10000;
    IF pg_major >= 18 THEN
        -- PostgreSQL 18+ → нативный uuidv7()
        EXECUTE '
            ALTER TABLE public.reports
            ADD COLUMN IF NOT EXISTS public_id uuid
            NOT NULL
            DEFAULT uuidv7();
        ';
    ELSIF pg_major >= 17 THEN
        -- PostgreSQL 17 → нет uuidv7 в ядре, fallback на генерирование в приложении
        -- или использование gen_random_uuid() (uuidv4).
        EXECUTE 'CREATE EXTENSION IF NOT EXISTS pgcrypto';
        EXECUTE '
            ALTER TABLE public.reports
            ADD COLUMN IF NOT EXISTS public_id uuid
            NOT NULL
            DEFAULT gen_random_uuid();
        ';
    ELSE
        -- PostgreSQL <=16 → стандартный UUIDv4
        EXECUTE 'CREATE EXTENSION IF NOT EXISTS pgcrypto';
        EXECUTE '
            ALTER TABLE public.reports
            ADD COLUMN IF NOT EXISTS public_id uuid
            NOT NULL
            DEFAULT gen_random_uuid();
        ';
    END IF;
END
$$;

CREATE OR REPLACE FUNCTION public.resolve_report_id(_workspace_id text DEFAULT NULL, _id integer DEFAULT NULL, _public_id uuid DEFAULT NULL)
    RETURNS integer
    LANGUAGE plpgsql
    STABLE
    AS $$
DECLARE
    result_id integer;
BEGIN
    IF _id IS NOT NULL THEN
        SELECT
            r.id INTO result_id
        FROM
            public.reports r
        WHERE
            r.id = _id
            AND (_workspace_id IS NULL
                OR r.creator_organization_id = _workspace_id);
        RETURN result_id;
    ELSIF _public_id IS NOT NULL THEN
        SELECT
            r.id INTO result_id
        FROM
            public.reports r
        WHERE
            r.public_id = _public_id
            AND (_workspace_id IS NULL
                OR r.creator_organization_id = _workspace_id);
        RETURN result_id;
    END IF;
    RETURN NULL;
END;
$$;