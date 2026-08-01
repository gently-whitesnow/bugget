-- Снимает версии функций, которые ниже пересоздаются из sql/functions/ с
-- изменённым набором параметров или другой TABLE-сигнатурой.
--
-- 1) patch_report_internal: добавляется параметр `_is_excluded_from_analytics`,
--    поэтому 4-параметровая сигнатура (с main) и предыдущая 5-параметровая
--    (если ветка уже накатывалась на dev) должны быть удалены до CREATE.
-- 2) get_report_internal / list_reports_internal: сигнатура совпадает, но
--    RETURNS TABLE расширяется колонкой `is_excluded_from_analytics`, что
--    приравнивается к смене типа возврата и не даёт обойтись CREATE OR REPLACE.
--
-- После этой миграции сборщик функций (sql/functions/*.sql, NullJournal)
-- создаёт актуальные тела.

DROP FUNCTION IF EXISTS public.patch_report_internal(int, text, integer, text);
DROP FUNCTION IF EXISTS public.patch_report_internal(int, text, integer, text, boolean);
DROP FUNCTION IF EXISTS public.get_report_internal(int);
DROP FUNCTION IF EXISTS public.list_reports_internal(int[]);
