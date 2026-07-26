-- Outbox table for beta-test-bot domain events.
-- See TECHSPEC §5.1 and ADR-20260423-beta-bot-domain-events-outbox.

CREATE TABLE IF NOT EXISTS public.domain_events (
    id                 bigserial   PRIMARY KEY,
    workspace_id       text        NOT NULL,
    aggregate_type     text        NOT NULL,
    aggregate_id       text        NOT NULL,
    event_type         text        NOT NULL,
    event_version      smallint    NOT NULL DEFAULT 1,
    payload            jsonb       NOT NULL,
    actor_user_id      text,
    actor_creator_type smallint,
    occurred_at        timestamptz NOT NULL DEFAULT now(),
    correlation_id     uuid
);

CREATE INDEX IF NOT EXISTS idx_domain_events_workspace_id_id ON public.domain_events (workspace_id, id);
CREATE INDEX IF NOT EXISTS idx_domain_events_aggregate ON public.domain_events (aggregate_type, aggregate_id, id);
CREATE INDEX IF NOT EXISTS idx_domain_events_event_type_id ON public.domain_events (event_type, id);
