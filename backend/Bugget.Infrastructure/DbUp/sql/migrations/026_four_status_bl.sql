ALTER TABLE public.comments
  ADD COLUMN creator_type smallint NOT NULL DEFAULT 0;


CREATE OR REPLACE FUNCTION public.create_comment_internal(_user_id text, _bug_id integer, _text text, _creator_type smallint)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        text text,
        creator_user_id text,
        creator_type smallint,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY INSERT INTO public.comments(bug_id, text, creator_user_id, creator_type)
        VALUES(_bug_id, _text, _user_id, _creator_type)
    RETURNING
        comments.id, comments.bug_id, comments.text, comments.creator_user_id, comments.creator_type, comments.created_at, comments.updated_at;
END;
$$;

DROP FUNCTION IF EXISTS public.list_comments_internal(_report_ids int[]);
CREATE OR REPLACE FUNCTION public.list_comments_internal(_report_ids int[])
    RETURNS TABLE(
        id int,
        bug_id int,
        text text,
        creator_user_id text,
        creator_type smallint,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE sql
    STABLE
    AS $$
    SELECT
        c.id,
        c.bug_id,
        c.text,
        c.creator_user_id,
        c.creator_type,
        c.created_at,
        c.updated_at
    FROM
        public.comments c
        JOIN public.bugs b ON c.bug_id = b.id
    WHERE
        b.report_id = ANY(_report_ids);
$$;