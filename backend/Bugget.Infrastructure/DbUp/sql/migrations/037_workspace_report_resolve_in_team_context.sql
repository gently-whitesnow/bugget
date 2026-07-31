-- Workspace-scoped reports (creator_team_id IS NULL) should stay accessible
-- from any team route within the same workspace, because beta-test UI lives
-- under /workspaces/{w}/teams/{t}#beta-test but creates workspace-level reports.

CREATE OR REPLACE FUNCTION public.resolve_report_id(
    _workspace_id text DEFAULT NULL,
    _team_id text DEFAULT NULL,
    _id integer DEFAULT NULL,
    _public_id uuid DEFAULT NULL,
    _team_report_id integer DEFAULT NULL
)
RETURNS TABLE(
    id integer,
    creator_team_id text,
    team_report_id integer
)
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    IF _id IS NOT NULL THEN
        RETURN QUERY
        SELECT
            r.id,
            r.creator_team_id,
            r.team_report_id
        FROM public.reports r
        WHERE r.id = _id
          AND (_workspace_id IS NULL OR r.creator_organization_id = _workspace_id)
          AND (
              _team_id IS NULL
              OR r.creator_team_id = _team_id
              OR r.creator_team_id IS NULL
          );
        RETURN;
    ELSIF _team_report_id IS NOT NULL THEN
        IF _team_id IS NOT NULL THEN
            RETURN QUERY
            SELECT
                r.id,
                r.creator_team_id,
                r.team_report_id
            FROM public.reports r
            WHERE r.team_report_id = _team_report_id
              AND (_workspace_id IS NULL OR r.creator_organization_id = _workspace_id)
              AND r.creator_team_id = _team_id;
            IF FOUND THEN
                RETURN;
            END IF;
        END IF;

        -- fallback: treat team_report_id as legacy global id
        RETURN QUERY
        SELECT
            r.id,
            r.creator_team_id,
            r.team_report_id
        FROM public.reports r
        WHERE r.id = _team_report_id
          AND (_workspace_id IS NULL OR r.creator_organization_id = _workspace_id)
          AND (
              _team_id IS NULL
              OR r.creator_team_id = _team_id
              OR r.creator_team_id IS NULL
          );
        RETURN;
    ELSIF _public_id IS NOT NULL THEN
        RETURN QUERY
        SELECT
            r.id,
            r.creator_team_id,
            r.team_report_id
        FROM public.reports r
        WHERE r.public_id = _public_id
          AND (_workspace_id IS NULL OR r.creator_organization_id = _workspace_id)
          AND (
              _team_id IS NULL
              OR r.creator_team_id = _team_id
              OR r.creator_team_id IS NULL
          );
        RETURN;
    END IF;
END;
$$;
