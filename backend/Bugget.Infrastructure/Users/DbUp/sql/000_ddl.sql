CREATE TABLE IF NOT EXISTS users(
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    external_id varchar(256) NOT NULL UNIQUE,
    name varchar(256) NOT NULL,
    image_url varchar(512),
    registration_date timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS workspaces(
    id int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name varchar(32) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS teams(
    id int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workspace_id int NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    name varchar(64) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (workspace_id, name)
);

CREATE TABLE IF NOT EXISTS workspaces_members(
    workspace_id int NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    user_id bigint NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role varchar(32) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (workspace_id, user_id)
);

CREATE TABLE IF NOT EXISTS teams_members(
    team_id int NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    user_id bigint NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (team_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_teams_members_team_id ON teams_members(team_id);

CREATE TABLE IF NOT EXISTS team_invites(
    id int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    team_id int UNIQUE NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    workspace_id int NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    token_hash bytea NOT NULL UNIQUE,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL
);
create unique index if not exists ux_team_invites_token_hash on team_invites(token_hash);
