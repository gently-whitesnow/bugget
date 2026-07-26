CREATE OR REPLACE FUNCTION public.create_report_v3(_user_id text, _title text, _team_id text DEFAULT NULL, _organization_id text DEFAULT NULL)
    RETURNS TABLE(
        id int,
        public_id uuid,
        title text,
        status int,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_user_id text,
        creator_team_id text,
        responsible_user_id text,
        past_responsible_user_id text)
    LANGUAGE plpgsql
    AS $$
DECLARE
    new_report_id integer;
BEGIN
    -- Создаём Report
    INSERT INTO public.reports(responsible_user_id, title, status, creator_user_id, creator_team_id, creator_organization_id, past_responsible_user_id)
        VALUES (_user_id, _title, 0, _user_id, _team_id, _organization_id, _user_id)
    RETURNING
        public.reports.id INTO new_report_id;
    -- Добавляем участников
    INSERT INTO public.report_participants(report_id, user_id)
        VALUES (new_report_id, _user_id);
    RETURN QUERY
    SELECT
        r.id,
        r.public_id,
        r.title,
        r.status,
        r.created_at,
        r.updated_at,
        r.creator_user_id,
        r.creator_team_id,
        r.responsible_user_id,
        r.past_responsible_user_id
    FROM
        public.reports AS r
    WHERE
        r.id = new_report_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.get_report_internal(_report_id int)
    RETURNS TABLE(
        id int,
        public_id uuid,
        title text,
        status int,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_user_id text,
        creator_team_id text,
        responsible_user_id text,
        past_responsible_user_id text)
    LANGUAGE plpgsql
    STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        r.id,
        r.public_id,
        r.title,
        r.status,
        r.created_at,
        r.updated_at,
        r.creator_user_id,
        r.creator_team_id,
        r.responsible_user_id,
        r.past_responsible_user_id
    FROM
        public.reports r
    WHERE
        r.id = _report_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.patch_report_internal(_report_id int, _title text DEFAULT NULL, _status integer DEFAULT NULL, _responsible_user_id text DEFAULT NULL)
    RETURNS TABLE(
        id integer,
        public_id uuid,
        title text,
        status integer,
        responsible_user_id text,
        past_responsible_user_id text,
        updated_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
    UPDATE
        public.reports AS r
    SET
        updated_at = now(),
        past_responsible_user_id = CASE WHEN _responsible_user_id IS NOT NULL THEN
            r.responsible_user_id
        ELSE
            r.past_responsible_user_id
        END,
        status = COALESCE(_status, r.status),
        title = COALESCE(_title, r.title),
        responsible_user_id = COALESCE(_responsible_user_id, r.responsible_user_id)
    WHERE
        r.id = _report_id;
    RETURN QUERY
    SELECT
        r.id,
        r.public_id,
        r.title,
        r.status,
        r.responsible_user_id,
        r.past_responsible_user_id,
        r.updated_at
    FROM
        public.reports AS r
    WHERE
        r.id = _report_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.get_bug_internal(_report_id int, _bug_id int)
    RETURNS TABLE(
        id integer,
        report_id int,
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

CREATE OR REPLACE FUNCTION public.create_bug_step_internal(_user_id text, _bug_id integer, _text text)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        step_number integer,
        text text,
        creator_user_id text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
DECLARE
    new_step_number integer;
BEGIN
    SELECT
        COALESCE(MAX(s.step_number), 0) + 1 INTO new_step_number
    FROM
        public.bug_steps s
    WHERE
        s.bug_id = _bug_id;
    RETURN QUERY INSERT INTO public.bug_steps(bug_id, step_number, text, creator_user_id)
        VALUES (_bug_id, new_step_number, _text, _user_id)
    RETURNING
        bug_steps.id, bug_steps.bug_id, bug_steps.step_number, bug_steps.text, bug_steps.creator_user_id, bug_steps.created_at, bug_steps.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.delete_bug_step_internal(_report_id integer, _bug_id integer, _step_id integer)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        step_number integer,
        text text,
        creator_user_id text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY DELETE FROM public.bug_steps s USING public.bugs b
    WHERE s.id = _step_id
        AND s.bug_id = _bug_id
        AND b.report_id = _report_id
    RETURNING
        s.id, s.bug_id, s.step_number, s.text, s.creator_user_id, s.created_at, s.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.patch_bug_step_internal(_report_id integer, _bug_id integer, _step_id integer, _text text)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        step_number integer,
        text text,
        creator_user_id text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY UPDATE
        public.bug_steps AS s
    SET
        text = COALESCE(_text, s.text),
        updated_at = now()
    FROM
        public.bugs b
    WHERE
        s.id = _step_id
        AND s.bug_id = _bug_id
        AND b.report_id = _report_id
    RETURNING
        s.id,
        s.bug_id,
        s.step_number,
        s.text,
        s.creator_user_id,
        s.created_at,
        s.updated_at;
END;
$$;

-- Перестановка порядка шагов
CREATE OR REPLACE FUNCTION public.update_bug_steps_order_internal(_report_id integer, _bug_id integer, _step_ids integer[])
    RETURNS TABLE(
        id integer,
        bug_id integer,
        step_number integer,
        text text,
        creator_user_id text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    WITH payload AS(
        SELECT
            step_id AS id,
            ord AS new_step_number
        FROM
            unnest(_step_ids)
            WITH ORDINALITY AS t(step_id,
            ord))
        UPDATE
            public.bug_steps s
        SET
            step_number = p.new_step_number,
            updated_at = now()
        FROM
            payload p
        WHERE
            s.id = p.id
            AND s.bug_id = _bug_id
            AND EXISTS(
                SELECT
                    1
                FROM
                    public.bugs b
                WHERE
                    b.id = s.bug_id
                    AND b.report_id = _report_id);
    RETURN QUERY
    SELECT
        s.id,
        s.bug_id,
        s.step_number,
        s.text,
        s.creator_user_id,
        s.created_at,
        s.updated_at
    FROM
        public.bug_steps s
        JOIN public.bugs b ON b.id = s.bug_id
    WHERE
        s.bug_id = _bug_id
        AND b.report_id = _report_id
    ORDER BY
        s.step_number;
END;
$$;

CREATE OR REPLACE FUNCTION public.list_bug_steps_internal(_report_id integer, _bug_id integer)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        step_number integer,
        text text,
        creator_user_id text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        s.id,
        s.bug_id,
        s.step_number,
        s.text,
        s.creator_user_id,
        s.created_at,
        s.updated_at
    FROM
        public.bug_steps s
        JOIN public.bugs b ON b.id = s.bug_id
    WHERE
        s.bug_id = _bug_id
        AND b.report_id = _report_id
    ORDER BY
        s.step_number;
END;
$$;

CREATE OR REPLACE FUNCTION public.create_bug_internal(_user_id text, _report_id int, _receive text, _expect text)
    RETURNS TABLE(
        id int,
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
    INSERT INTO public.bugs(report_id, receive, expect, creator_user_id, status)
        VALUES (_report_id, _receive, _expect, _user_id, 0)
    RETURNING
        public.bugs.id INTO new_bug_id;
    RETURN QUERY
    SELECT
        b.id,
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

CREATE OR REPLACE FUNCTION public.patch_bug_internal(_bug_id int, _report_id int, _receive text, _expect text, _status int)
    RETURNS TABLE(
        id int,
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
        updated_at = now()
    WHERE
        b.id = _bug_id
        AND b.report_id = _report_id;
    RETURN QUERY
    SELECT
        b.id,
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

CREATE OR REPLACE FUNCTION public.create_comment_internal(_user_id text, _bug_id integer, _text text)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        text text,
        creator_user_id text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY INSERT INTO public.comments(bug_id, text, creator_user_id)
        VALUES(_bug_id, _text, _user_id)
    RETURNING
        comments.id, comments.bug_id, comments.text, comments.creator_user_id, comments.created_at, comments.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.delete_comment_internal(_user_id text, _report_id integer, _bug_id integer, _comment_id integer)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        text text,
        creator_user_id text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY DELETE FROM public.comments c USING public.bugs b
    WHERE c.id = _comment_id
        AND b.report_id = _report_id
        AND c.bug_id = _bug_id
        AND c.creator_user_id = _user_id
    RETURNING
        c.id, c.bug_id, c.text, c.creator_user_id, c.created_at, c.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.update_comment_internal(_user_id text, _report_id integer, _bug_id integer, _comment_id integer, _new_text text)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        text text,
        creator_user_id text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY UPDATE
        public.comments c
    SET
        text = _new_text,
        updated_at = now()
    FROM
        public.bugs b
    WHERE
        c.id = _comment_id
        AND c.bug_id = _bug_id
        AND c.creator_user_id = _user_id
        AND b.id = c.bug_id
        AND b.report_id = _report_id
    RETURNING
        c.id,
        c.bug_id,
        c.text,
        c.creator_user_id,
        c.created_at,
        c.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.get_bug_attachment_internal(_report_id int, _bug_id int, _attachment_id int)
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
        created_at timestamp with time zone,
        bug_id int,
        path text)
    LANGUAGE plpgsql
    STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
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
        a.created_at,
        a.bug_id,
        a.path
    FROM
        public.attachments a
        JOIN public.bugs b ON(a.entity_id = b.id
                OR a.bug_id = b.id)
    WHERE
        a.id = _attachment_id
        AND b.id = _bug_id
        AND b.report_id = _report_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.get_comment_attachment_internal(_report_id int, _bug_id int, _comment_id int, _attachment_id int)
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
    STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
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
        a.created_at
    FROM
        public.attachments a
        JOIN public.comments c ON a.entity_id = c.id
        JOIN public.bugs b ON c.bug_id = b.id
    WHERE
        a.id = _attachment_id
        AND c.id = _comment_id
        AND c.bug_id = _bug_id
        AND b.report_id = _report_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.get_bug_step_attachment_internal(_report_id int, _bug_id int, _step_id int, _attachment_id int)
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
    STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
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
        a.created_at
    FROM
        public.attachments a
        JOIN public.bug_steps s ON a.entity_id = s.id
        JOIN public.bugs b ON s.bug_id = b.id
    WHERE
        a.id = _attachment_id
        AND s.id = _step_id
        AND s.bug_id = _bug_id
        AND b.report_id = _report_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.delete_bug_attachment_internal(_report_id int, _bug_id int, _attachment_id int)
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
    RETURN QUERY DELETE FROM public.attachments a USING public.bugs b
    WHERE a.entity_id = b.id
        AND b.id = _bug_id
        AND b.report_id = _report_id
        AND a.id = _attachment_id
    RETURNING
        a.id, a.attach_type, a.entity_id, a.storage_key, a.storage_kind, a.creator_user_id, a.length_bytes, a.file_name, a.mime_type, a.has_preview, a.is_gzip_compressed, a.created_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.delete_bug_step_attachment_internal(_report_id int, _bug_id int, _step_id int, _attachment_id int)
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
    RETURN QUERY DELETE FROM public.attachments a USING public.bug_steps s, public.bugs b
    WHERE a.entity_id = s.id
        AND s.bug_id = b.id
        AND a.id = _attachment_id
        AND s.id = _step_id
        AND s.bug_id = _bug_id
        AND b.report_id = _report_id
        AND a.attach_type = 3
    RETURNING
        a.id, a.attach_type, a.entity_id, a.storage_key, a.storage_kind, a.creator_user_id, a.length_bytes, a.file_name, a.mime_type, a.has_preview, a.is_gzip_compressed, a.created_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.delete_comment_attachment_internal(_report_id int, _bug_id int, _comment_id int, _attachment_id int)
    RETURNS TABLE(
        id integer,
        attach_type integer,
        entity_id integer,
        storage_key text,
        storage_kind integer,
        creator_user_id text,
        length_bytes bigint,
        file_name text,
        mime_type text,
        has_preview boolean,
        is_gzip_compressed boolean,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY DELETE FROM public.attachments a USING public.comments c, public.bugs b
    WHERE a.entity_id = c.id
        AND c.bug_id = b.id
        AND a.id = _attachment_id
        AND c.id = _comment_id
        AND c.bug_id = _bug_id
        AND b.report_id = _report_id
        AND a.attach_type = 2
    RETURNING
        a.id, a.attach_type, a.entity_id, a.storage_key, a.storage_kind, a.creator_user_id, a.length_bytes, a.file_name, a.mime_type, a.has_preview, a.is_gzip_compressed, a.created_at, a.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.get_bug_attachments_count_internal(_report_id int, _bug_id int, _attach_type int)
    RETURNS int
    LANGUAGE plpgsql
    STABLE
    AS $$
BEGIN
    RETURN(
        SELECT
            COUNT(*)
        FROM
            public.attachments a
            JOIN public.bugs b ON a.entity_id = b.id
        WHERE
            b.id = _bug_id
            AND b.report_id = _report_id
            AND a.attach_type = _attach_type);
END;
$$;

CREATE OR REPLACE FUNCTION public.get_comment_attachments_count_internal(_user_id text, _report_id int, _bug_id int, _comment_id int)
    RETURNS int
    LANGUAGE plpgsql
    STABLE
    AS $$
DECLARE
    _comment_attach_type int := 2;
BEGIN
    RETURN (
        SELECT
            COUNT(*)
        FROM
            public.attachments a
            JOIN public.comments c ON a.entity_id = c.id
            JOIN public.bugs b ON c.bug_id = b.id
        WHERE
            a.entity_id = _comment_id
            AND c.creator_user_id = _user_id
            AND a.attach_type = _comment_attach_type
            AND c.bug_id = _bug_id
            AND b.report_id = _report_id);
END;
$$;

CREATE OR REPLACE FUNCTION public.get_bug_step_attachments_count_internal(_report_id integer, _bug_id integer, _step_id integer)
    RETURNS int
    LANGUAGE plpgsql
    STABLE
    AS $$
DECLARE
    _bug_step_attach_type int := 3;
BEGIN
    RETURN (
        SELECT
            COUNT(*)
        FROM
            public.attachments a
            JOIN public.bug_steps s ON a.entity_id = s.id
            JOIN public.bugs b ON s.bug_id = b.id
        WHERE
            a.entity_id = _step_id
            AND a.attach_type = _bug_step_attach_type
            AND s.bug_id = _bug_id
            AND b.report_id = _report_id);
END;
$$;

CREATE OR REPLACE FUNCTION public.create_report_link_internal(_report_id int, _link text, _name text)
    RETURNS TABLE(
        id int,
        report_id int,
        link text,
        name text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY INSERT INTO public.report_links(report_id, link, name)
        VALUES(_report_id, _link, _name)
    RETURNING
        report_links.id, report_links.report_id, report_links.link, report_links.name, report_links.created_at, report_links.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.update_report_link_internal(_report_id int, _link_id int, _link text, _name text)
    RETURNS TABLE(
        id int,
        report_id int,
        link text,
        name text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY UPDATE
        public.report_links rl
    SET
        link = COALESCE(_link, rl.link),
        name = COALESCE(_name, rl.name),
        updated_at = NOW()
    WHERE
        rl.id = _link_id
        AND rl.report_id = _report_id
    RETURNING
        rl.id,
        rl.report_id,
        rl.link,
        rl.name,
        rl.created_at,
        rl.updated_at;
END;
$$;

CREATE OR REPLACE FUNCTION public.delete_report_link_internal(_report_id int, _link_id int)
    RETURNS TABLE(
        id int,
        report_id int,
        link text,
        name text,
        created_at timestamptz,
        updated_at timestamptz)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY DELETE FROM public.report_links rl
    WHERE rl.id = _link_id
        AND rl.report_id = _report_id
    RETURNING
        rl.id,
        rl.report_id,
        rl.link,
        rl.name,
        rl.created_at,
        rl.updated_at;
END;
$$;

