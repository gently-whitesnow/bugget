/**
 * Сборка query-строки. Имена параметров — часть публичного контракта (ADR-0009)
 * и уходят в URL как есть. Массив кладётся повторяющимся ключом (`a=1&a=2`):
 * так читает бекенд, а axios `params` дал бы `a[]=1` и сменил бы провод.
 * `null` и `undefined` пропускаются; пустую строку нормализует вызывающий код.
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
