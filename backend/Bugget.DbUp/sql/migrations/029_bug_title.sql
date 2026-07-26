ALTER TABLE public.bugs ADD COLUMN title text NULL;

DROP FUNCTION IF EXISTS public.list_bugs_internal(int[]);
DROP FUNCTION IF EXISTS public.get_bug_internal(int, int);
DROP FUNCTION IF EXISTS public.create_bug_internal(text, int, text, text);
DROP FUNCTION IF EXISTS public.create_bug_internal(text, int, text, text, text);
DROP FUNCTION IF EXISTS public.patch_bug_internal(int, int, text, text, int);
DROP FUNCTION IF EXISTS public.patch_bug_internal(int, int, text, text, int, text);

CREATE OR REPLACE FUNCTION public.list_bugs_internal(_report_ids int[])
    RETURNS TABLE(
        id int,
        report_id int,
        title text,
        receive text,
        expect text,
        status int,
        creator_user_id text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE sql
    STABLE
    AS $$
    SELECT
        b.id,
        b.report_id,
        b.title,
        b.receive,
        b.expect,
        b.status,
        b.creator_user_id,
        b.created_at,
        b.updated_at
    FROM
        public.bugs b
    WHERE
        b.report_id = ANY(_report_ids);
$$;

CREATE OR REPLACE FUNCTION public.get_bug_internal(_report_id int, _bug_id int)
    RETURNS TABLE(
        id integer,
        report_id int,
        title text,
        receive text,
        expect text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_user_id text,
        status int)
    LANGUAGE plpgsql
    STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        b.id,
        b.report_id,
        b.title,
        b.receive,
        b.expect,
        b.created_at,
        b.updated_at,
        b.creator_user_id,
        b.status
    FROM
        public.bugs b
    WHERE
        b.report_id = _report_id
        AND b.id = _bug_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.create_bug_internal(_user_id text, _report_id int, _receive text, _expect text, _title text DEFAULT NULL)
    RETURNS TABLE(
        id int,
        title text,
        receive text,
        expect text,
        status int,
        creator_user_id text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
DECLARE
    new_bug_id integer;
BEGIN
    INSERT INTO public.bugs(report_id, receive, expect, title, creator_user_id, status)
        VALUES (_report_id, _receive, _expect, _title, _user_id, 0)
    RETURNING
        public.bugs.id INTO new_bug_id;
    RETURN QUERY
    SELECT
        b.id,
        b.title,
        b.receive,
        b.expect,
        b.status,
        b.creator_user_id,
        b.created_at,
        b.updated_at
    FROM
        public.bugs b
    WHERE
        b.id = new_bug_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.patch_bug_internal(_bug_id int, _report_id int, _receive text, _expect text, _status int, _title text DEFAULT NULL)
    RETURNS TABLE(
        id int,
        title text,
        receive text,
        expect text,
        status int,
        updated_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
    UPDATE
        public.bugs AS b
    SET
        receive = COALESCE(_receive, b.receive),
        expect = COALESCE(_expect, b.expect),
        status = COALESCE(_status, b.status),
        title = COALESCE(_title, b.title),
        updated_at = now()
    WHERE
        b.id = _bug_id
        AND b.report_id = _report_id;
    RETURN QUERY
    SELECT
        b.id,
        b.title,
        b.receive,
        b.expect,
        b.status,
        b.updated_at
    FROM
        public.bugs b
    WHERE
        b.id = _bug_id
        AND b.report_id = _report_id;
END;
$$;
