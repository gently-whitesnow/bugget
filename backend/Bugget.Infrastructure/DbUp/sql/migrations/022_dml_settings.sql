-- Служебные функции для выборки и upsert настроек

create or replace function public.list_workspace_settings(p_workspace_id text)
returns setof public.workspace_settings
language sql
as $$
    select * from public.workspace_settings
    where workspace_id = p_workspace_id;
$$;

create or replace function public.list_team_settings(p_team_id text)
returns setof public.team_settings
language sql
as $$
    select * from public.team_settings
    where team_id = p_team_id;
$$;

create or replace function public.list_user_settings(p_user_id text)
returns setof public.user_settings
language sql
as $$
    select * from public.user_settings
    where user_id = p_user_id;
$$;

create or replace function public.upsert_workspace_setting(
    p_workspace_id text,
    p_feature_key text,
    p_field_key text,
    p_field_value text
) returns public.workspace_settings
language plpgsql
as $$
declare
    v_workspace_setting public.workspace_settings;
begin
    update public.workspace_settings
    set feature_key = p_feature_key,
        field_key = p_field_key,
        field_value = p_field_value,
        updated_at = now()
    where workspace_id = p_workspace_id
      and feature_key = p_feature_key
      and field_key = p_field_key
    returning * into v_workspace_setting;

    if not found then
        insert into public.workspace_settings (workspace_id, feature_key, field_key, field_value)
        values (p_workspace_id, p_feature_key, p_field_key, p_field_value)
        returning * into v_workspace_setting;
    end if;

    return v_workspace_setting;
end;
$$;

create or replace function public.upsert_team_setting(
    p_team_id text,
    p_feature_key text,
    p_field_key text,
    p_field_value text
) returns public.team_settings
language plpgsql
as $$
declare
    v_team_setting public.team_settings;
begin
    update public.team_settings
    set feature_key = p_feature_key,
        field_key = p_field_key,
        field_value = p_field_value,
        updated_at = now()
    where team_id = p_team_id
      and feature_key = p_feature_key
      and field_key = p_field_key
    returning * into v_team_setting;

    if not found then
        insert into public.team_settings (team_id, feature_key, field_key, field_value)
        values (p_team_id, p_feature_key, p_field_key, p_field_value)
        returning * into v_team_setting;
    end if;

    return v_team_setting;
end;
$$;

create or replace function public.upsert_user_setting(
    p_user_id text,
    p_feature_key text,
    p_field_key text,
    p_field_value text
) returns public.user_settings
language plpgsql
as $$
declare
    v_user_setting public.user_settings;
begin
    update public.user_settings
    set feature_key = p_feature_key,
        field_key = p_field_key,
        field_value = p_field_value,
        updated_at = now()
    where user_id = p_user_id
      and feature_key = p_feature_key
      and field_key = p_field_key
    returning * into v_user_setting;

    if not found then
        insert into public.user_settings (user_id, feature_key, field_key, field_value)
        values (p_user_id, p_feature_key, p_field_key, p_field_value)
        returning * into v_user_setting;
    end if;

    return v_user_setting;
end;
$$;

-- Replace: удалить все для владельца и вставить новые значения одним вызовом

create or replace function public.replace_workspace_settings_section(
    p_workspace_id text,
    p_feature_key text,
    p_field_key text,
    p_field_values text[]
) returns setof public.workspace_settings
language plpgsql
as $$
declare
    v_len int := coalesce(array_length(p_field_values, 1), 0);
begin
    delete from public.workspace_settings
    where workspace_id = p_workspace_id
      and feature_key = p_feature_key
      and field_key = p_field_key;

    if v_len = 0 then
        return;
    end if;

    return query
    insert into public.workspace_settings (workspace_id, feature_key, field_key, field_value)
    select p_workspace_id, p_feature_key, p_field_key, fv.field_value
    from unnest(p_field_values) with ordinality fv(field_value, ord)
    returning *;
end;
$$;

create or replace function public.replace_team_settings_section(
    p_team_id text,
    p_feature_key text,
    p_field_key text,
    p_field_values text[]
) returns setof public.team_settings
language plpgsql
as $$
declare
    v_len int := coalesce(array_length(p_field_values, 1), 0);
begin
    delete from public.team_settings
    where team_id = p_team_id
      and feature_key = p_feature_key
      and field_key = p_field_key;

    if v_len = 0 then
        return;
    end if;

    return query
    insert into public.team_settings (team_id, feature_key, field_key, field_value)
    select p_team_id, p_feature_key, p_field_key, fv.field_value
    from unnest(p_field_values) with ordinality fv(field_value, ord)
    returning *;
end;
$$;

create or replace function public.replace_user_settings_section(
    p_user_id text,
    p_feature_key text,
    p_field_key text,
    p_field_values text[]
) returns setof public.user_settings
language plpgsql
as $$
declare
    v_len int := coalesce(array_length(p_field_values, 1), 0);
begin
    delete from public.user_settings
    where user_id = p_user_id
      and feature_key = p_feature_key
      and field_key = p_field_key;

    if v_len = 0 then
        return;
    end if;

    return query
    insert into public.user_settings (user_id, feature_key, field_key, field_value)
    select p_user_id, p_feature_key, p_field_key, fv.field_value
    from unnest(p_field_values) with ordinality fv(field_value, ord)
    returning *;
end;
$$;