export const capitalize = (str: string) =>
  str.substr(0, 1).toUpperCase() + str.substr(1);

export const unCapitalize = (str: string) =>
  str.substr(0, 1).toLowerCase() + str.substr(1);

export const snakeToPascal = (str: string) =>
  str.split("_").map(capitalize).join("");

export const snakeToCamel = (str: string) => unCapitalize(snakeToPascal(str));

/** `PascalCase`/`camelCase` → `snake_case`. */
export const pascalToSnake = (str: string) =>
  str
    .split(/(?=[A-Z])/)
    .map((str: string) => str.toLowerCase())
    .join("_");

export function transformObjKeysRecursively(
  obj: unknown,
  func: (key: string) => string
): unknown {
  if (obj === null || typeof obj !== "object") {
    return obj;
  }

  if (Array.isArray(obj)) {
    return obj.map((item) => transformObjKeysRecursively(item, func));
  }

  const result: Record<string, unknown> = {};
  for (const key in obj as Record<string, unknown>) {
    const value = (obj as Record<string, unknown>)[key];
    result[func(key)] = transformObjKeysRecursively(value, func);
  }

  return result;
}

export const convertObjectToCamel = (obj: unknown) =>
  transformObjKeysRecursively(obj, snakeToCamel);

export const convertObjectToSnake = (obj: unknown) =>
  transformObjKeysRecursively(obj, pascalToSnake);

export const simpleArrayToDict = (array: unknown, id = "id") => {
  if (!array || !Array.isArray(array)) {
    return array;
  }

  return array.reduce((result, item) => {
    result[item[id]] = item;
    return result;
  }, {});
};
