create or replace function list_workspaces()
    returns setof workspaces
    language plpgsql
    as $$
begin
    return query select * from workspaces;
end;
$$;

CREATE OR REPLACE FUNCTION create_workspace(p_name text)
    RETURNS SETOF workspaces
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    INSERT INTO workspaces(name)
        VALUES (p_name)
    RETURNING *;
END;
$$;