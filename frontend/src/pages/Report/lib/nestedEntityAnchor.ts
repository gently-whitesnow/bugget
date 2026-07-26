const escapeRegExp = (value: string): string =>
  value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

type NestedEntityAnchor = {
  getElementId: (parentId: number, entityId: number) => string;
  getAnchorHref: (parentId: number, entityId: number) => string;
  hashPattern: RegExp;
};

type SingleEntityAnchor = {
  getElementId: (entityId: number) => string;
  getAnchorHref: (entityId: number) => string;
  hashPattern: RegExp;
};

type Options = {
  includeParentId?: boolean;
};

export function createNestedEntityAnchor(prefix: string): NestedEntityAnchor;
export function createNestedEntityAnchor(
  prefix: string,
  options: { includeParentId: false }
): SingleEntityAnchor;
/**
 * Создает набор утилит для якорей вида:
 * - id: `${prefix}-${parentId}-${entityId}`
 * - href: `#${prefix}-${parentId}-${entityId}`
 * - hashPattern: /^#${prefix}-(\d+)-(\d+)$/
 */
export function createNestedEntityAnchor(
  prefix: string,
  options: Options = {}
): NestedEntityAnchor | SingleEntityAnchor {
  const { includeParentId = true } = options;
  const safePrefix = escapeRegExp(prefix);

  if (!includeParentId) {
    const getElementId = (entityId: number): string => `${prefix}-${entityId}`;

    return {
      getElementId,
      getAnchorHref: (entityId: number): string => `#${getElementId(entityId)}`,
      hashPattern: new RegExp(`^#${safePrefix}-(\\d+)$`),
    };
  }

  const getElementId = (parentId: number, entityId: number): string =>
    `${prefix}-${parentId}-${entityId}`;

  return {
    getElementId,
    getAnchorHref: (parentId: number, entityId: number): string =>
      `#${getElementId(parentId, entityId)}`,
    hashPattern: new RegExp(`^#${safePrefix}-(\\d+)-(\\d+)$`),
  };
}
