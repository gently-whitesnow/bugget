CREATE TABLE IF NOT EXISTS public.report_links
(
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    report_id integer NOT NULL REFERENCES public.reports(id) ON DELETE CASCADE,
    link text NOT NULL,
    name text NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION public.list_report_links_internal(_report_ids int[])
    RETURNS TABLE(
        id int,
        report_id int,
        link text,
        name text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone)
    LANGUAGE sql
    STABLE
    AS $$
    SELECT
        rl.id,
        rl.report_id,
        rl.link,
        rl.name,
        rl.created_at,
        rl.updated_at
    FROM
        public.report_links rl
    WHERE
        rl.report_id = ANY(_report_ids);
$$;
