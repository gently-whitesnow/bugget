create or replace function delete_team_member(p_user_id bigint, p_team_id int)
    returns void
    language plpgsql
    as $$
begin
    delete from teams_members
    where user_id = p_user_id
    and team_id = p_team_id;
end;
$$;

create or replace function delete_workspace_member(p_user_id bigint, p_workspace_id int)
    returns void
    language plpgsql
    as $$
begin
    delete from workspaces_members
    where user_id = p_user_id
    and workspace_id = p_workspace_id;
end;
$$;

create or replace function delete_workspace(p_workspace_id int)
    returns void
    language plpgsql
    as $$
begin
    delete from workspaces
    where id = p_workspace_id;
end;
$$;