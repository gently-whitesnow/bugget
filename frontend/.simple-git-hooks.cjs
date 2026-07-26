/** @type {Record<string, string>} */
module.exports = {
  "pre-commit":
    "sh -c 'cd \"$(git rev-parse --show-toplevel)/frontend\" && npx lint-staged && npm run build && npm run lint && npx steiger ./src --fail-on-warnings'",
};


