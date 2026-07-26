create table if not exists public.team_settings (
    id integer generated always as identity primary key,
    team_id text not null,
    feature_key text not null,
    field_key text not null,
    field_value text not null,
    created_at timestamp with time zone not null default now(),
    updated_at timestamp with time zone not null default now()
);

create index if not exists idx_team_settings_team_id on public.team_settings (team_id);

create table if not exists public.workspace_settings (
    id integer generated always as identity primary key,
    workspace_id text not null,
    feature_key text not null,
    field_key text not null,
    field_value text not null,
    created_at timestamp with time zone not null default now(),
    updated_at timestamp with time zone not null default now()
);

create index if not exists idx_workspace_settings_workspace_id on public.workspace_settings (workspace_id);

create table if not exists public.user_settings (
    id integer generated always as identity primary key,
    user_id text not null,
    feature_key text not null,
    field_key text not null,
    field_value text not null,
    created_at timestamp with time zone not null default now(),
    updated_at timestamp with time zone not null default now()
);

create index if not exists idx_user_settings_user_id on public.user_settings (user_id);