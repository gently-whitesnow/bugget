import { FlatCompat } from '@eslint/eslintrc';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { globalIgnores } from 'eslint/config';

const compat = new FlatCompat({
  baseDirectory: dirname(fileURLToPath(import.meta.url)),
});

const config = [
  ...compat.extends('next/core-web-vitals'),
  globalIgnores(['.next/**', 'out/**']),
];

export default config;
