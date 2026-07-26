-- Cursor для polling-консьюмера `domain_events`. PK = consumer_name
-- (без workspace_id): локальный консьюмер тянет хвост одним SELECT'ом
-- `WHERE id > @cursor`.
--
-- Bootstrap: стартуем с MAX(id), исторический хвост в read-model не
-- попадает. ON CONFLICT DO NOTHING — миграция идемпотентна.

CREATE TABLE IF NOT EXISTS public.domain_events_cursor (
    consumer_name   text         PRIMARY KEY,
    last_event_id   bigint       NOT NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now()
);

INSERT INTO public.domain_events_cursor (consumer_name, last_event_id)
SELECT 'bugget-analytics', COALESCE(MAX(id), 0)
FROM public.domain_events
ON CONFLICT (consumer_name) DO NOTHING;
