-- Idempotency cache for external-author _internal endpoints (Idempotency-Key).
-- See TECHSPEC §4.3.1 and ADR-20260423-external-author-internal-api.

CREATE TABLE IF NOT EXISTS public.idempotency_cache (
    key           text        PRIMARY KEY,
    response_json jsonb       NOT NULL,
    expires_at    timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_idempotency_cache_expires_at ON public.idempotency_cache (expires_at);
