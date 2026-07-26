export const buildItemIdSet = <T>(
  items: T[],
  getId: (item: T) => number | undefined
): Set<number> => {
  const ids = new Set<number>();

  for (const item of items) {
    const id = getId(item);
    if (typeof id === "number") {
      ids.add(id);
    }
  }

  return ids;
};

export const parseHashNumbers = (
  hash: string,
  hashPattern: RegExp
): number[] | null => {
  const match = hash.match(hashPattern);
  if (!match) return null;

  const numbers = match.slice(1).map((value) => Number(value));
  if (numbers.some((value) => !Number.isFinite(value))) {
    return null;
  }

  return numbers;
};
