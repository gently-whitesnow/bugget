/**
 * Сборка query-строки из типизированного объекта параметров операции.
 *
 * Имена параметров берутся из сгенерированных `operations[...]["parameters"]["query"]`,
 * поэтому опечатка в имени — ошибка компиляции, а не 400 в рантайме. Сами имена
 * конверсию регистра не проходят: их camelCase — часть публичного контракта
 * (ADR-0009), и здесь они уходят в URL как есть.
 *
 * Массив кладётся повторяющимся ключом (`reportStatuses=0&reportStatuses=2`) —
 * так его читает бекенд и так его писал рукописный код. Через `params` axios
 * сериализовал бы его как `reportStatuses[]=0`, то есть сменил бы провод.
 *
 * `null` и `undefined` пропускаются: отсутствие параметра — «фильтра нет».
 * Пустую строку вызывающий код нормализует сам (`value || undefined`), если для
 * его ручки пустой фильтр значит «параметра нет».
 */

export type QueryPrimitive = string | number | boolean;

export type QueryValue =
  | QueryPrimitive
  | readonly QueryPrimitive[]
  | null
  | undefined;

export const buildQueryString = (
  params: Readonly<Record<string, QueryValue>>
): string => {
  const search = new URLSearchParams();

  for (const [name, value] of Object.entries(params)) {
    if (value === null || value === undefined) continue;

    if (Array.isArray(value)) {
      for (const item of value) search.append(name, String(item));
      continue;
    }

    search.append(name, String(value as QueryPrimitive));
  }

  return search.toString();
};
