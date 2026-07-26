/**
 * Правило no-restricted-syntax: запрет присвоения в innerHTML без санитизации.
 * Использование в eslint.config: "no-restricted-syntax": ["error", noUnsafeInnerHtmlOption]
 */
export const noUnsafeInnerHtmlOption = {
  selector:
    "AssignmentExpression[left.type='MemberExpression'][left.property.name='innerHTML']:not([right.type='CallExpression'][right.callee.name=/sanitize/i]):not([right.type='CallExpression'][right.callee.property.name=/sanitize/i])",
  message:
    "innerHTML must be assigned only with a sanitized value (e.g. sanitizeEditorHtml(...) or DOMPurify.sanitize(...)).",
};
