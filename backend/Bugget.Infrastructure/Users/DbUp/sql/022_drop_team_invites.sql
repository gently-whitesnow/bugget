-- Сценарий TTL-ссылок в команду (`team_invites`) в продукте не используется: нынешние
-- ссылки на вступление работают без него, HTTP/UI/SignalR его не вызывают. Владелец
-- разрешил удалить объекты сразу, без архива и переходного окна совместимости.
--
-- Историю переписывать нельзя: `000_ddl.sql` и `005_team_invites.sql` остаются как были,
-- а существующая база выбирает эту forward-only миграцию.
--
-- Порядок важен: функции возвращают SETOF team_invites, поэтому таблица удаляется после
-- них. CASCADE намеренно не используется — неизвестная зависимость обязана остановить
-- миграцию, а не быть удалённой молча. Индекс, ограничения и строки уходят с таблицей.
DROP FUNCTION IF EXISTS create_team_invite(int, int, bytea, timestamptz);

DROP FUNCTION IF EXISTS update_team_invite(int, int, bytea, timestamptz);

DROP FUNCTION IF EXISTS get_team_invite(int);

DROP FUNCTION IF EXISTS delete_team_invite(int, int);

DROP FUNCTION IF EXISTS accept_team_invite(bytea);

DROP TABLE IF EXISTS team_invites;
