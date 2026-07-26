-- Add audience column to comments for beta-test-bot external dialog support.
-- Enum: Internal=0 (default, legacy), External=1 (see Bugget.Entities/BO/Common/CommentAudience.cs).

ALTER TABLE public.comments ADD COLUMN IF NOT EXISTS audience smallint NOT NULL DEFAULT 0;

DROP FUNCTION IF EXISTS public.create_comment_internal(text, integer, text, smallint);
CREATE OR REPLACE FUNCTION public.create_comment_internal(_user_id text, _bug_id integer, _text text, _creator_type smallint, _audience smallint)
    RETURNS TABLE(
        id integer,
        bug_id integer,
        text text,
        creator_user_id text,
        creator_type smallint,
        audience smallint,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY INSERT INTO public.comments(bug_id, text, creator_user_id, creator_type, audience)
        VALUES(_bug_id, _text, _user_id, _creator_type, _audience)
    RETURNING
        comments.id, comments.bug_id, comments.text, comments.creator_user_id, comments.creator_type, comments.audience, comments.created_at, comments.updated_at;
END;
$$;

DROP FUNCTION IF EXISTS public.list_comments_internal(int[]);
CREATE OR REPLACE FUNCTION public.list_comments_internal(_report_ids int[])
    RETURNS TABLE(
        id int,
        bug_id int,
        text text,
        creator_user_id text,
        creator_type smallint,
        audience smallint,
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
        c.audience,
        c.created_at,
        c.updated_at
    FROM
        public.comments c
        JOIN public.bugs b ON c.bug_id = b.id
    WHERE
        b.report_id = ANY(_report_ids);
$$;
