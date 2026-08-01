-- Add creator_type column to reports and bugs for beta-test-bot external author support.
-- Enum: User=0 (default), System=1, TgBetaTester=2 (see Bugget.Entities/BO/Common/CreatorType.cs).

ALTER TABLE public.reports ADD COLUMN IF NOT EXISTS creator_type smallint NOT NULL DEFAULT 0;
ALTER TABLE public.bugs ADD COLUMN IF NOT EXISTS creator_type smallint NOT NULL DEFAULT 0;

DROP FUNCTION IF EXISTS public.create_report_v3(text, text, text, text);
CREATE OR REPLACE FUNCTION public.create_report_v3(_user_id text, _title text, _team_id text DEFAULT NULL, _organization_id text DEFAULT NULL)
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
        creator_type smallint)
    LANGUAGE plpgsql
    AS $$
DECLARE
    new_report_id integer;
    new_team_report_id integer;
BEGIN
    IF _team_id IS NOT NULL THEN
        INSERT INTO public.report_team_counters(team_id, last_report_id)
            VALUES (_team_id, 1)
        ON CONFLICT (team_id) DO UPDATE
            SET last_report_id = public.report_team_counters.last_report_id + 1
        RETURNING last_report_id INTO new_team_report_id;
    END IF;

    INSERT INTO public.reports(responsible_user_id, title, status, creator_user_id, creator_team_id, creator_organization_id, past_responsible_user_id, team_report_id)
        VALUES (_user_id, _title, 0, _user_id, _team_id, _organization_id, _user_id, new_team_report_id)
    RETURNING
        public.reports.id INTO new_report_id;
    INSERT INTO public.report_participants(report_id, user_id)
        VALUES (new_report_id, _user_id);
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
        r.creator_type
    FROM
        public.reports AS r
    WHERE
        r.id = new_report_id;
END;
$$;

DROP FUNCTION IF EXISTS public.get_report_internal(int);
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
        creator_type smallint)
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
        r.creator_type
    FROM
        public.reports r
    WHERE
        r.id = _report_id;
END;
$$;

DROP FUNCTION IF EXISTS public.list_reports_internal(int[]);
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
        creator_type smallint)
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
        r.creator_type
    FROM
        public.reports r
    WHERE
        r.id = ANY(_report_ids)
$$;

DROP FUNCTION IF EXISTS public.list_bugs_internal(int[]);
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
        updated_at timestamp with time zone,
        creator_type smallint)
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
        b.updated_at,
        b.creator_type
    FROM
        public.bugs b
    WHERE
        b.report_id = ANY(_report_ids);
$$;

DROP FUNCTION IF EXISTS public.get_bug_internal(int, int);
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
        status int,
        creator_type smallint)
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
        b.status,
        b.creator_type
    FROM
        public.bugs b
    WHERE
        b.report_id = _report_id
        AND b.id = _bug_id;
END;
$$;

DROP FUNCTION IF EXISTS public.create_bug_internal(text, int, text, text, text);
CREATE OR REPLACE FUNCTION public.create_bug_internal(_user_id text, _report_id int, _receive text, _expect text, _title text DEFAULT NULL)
    RETURNS TABLE(
        id int,
        title text,
        receive text,
        expect text,
        status int,
        creator_user_id text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_type smallint)
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
        b.updated_at,
        b.creator_type
    FROM
        public.bugs b
    WHERE
        b.id = new_bug_id;
END;
$$;
