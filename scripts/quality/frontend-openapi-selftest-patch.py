#!/usr/bin/env python3
"""Патчи контракта settings для самопроверки гейта frontend-contracts.

Ломает поле `user_sections` в песочнице разными способами — так, чтобы каждый
способ доехал до сгенерированного `.d.ts` в своей форме:

  caps     `user_ID_sections`   — некавыченное имя вне канона провода;
  hyphen   `'user-sections'`    — openapi-typescript кавычит: `"user-sections"`;
  digit    `'1sections'`        — то же, плюс ведущая цифра;
  freekey  свободный словарь    — в `.d.ts` это индексная сигнатура.

Живёт отдельным файлом, а не heredoc'ом внутри bash: heredoc с YAML-отступами
внутри shell-функции читается хуже, чем сам патч.

Использование: frontend-openapi-selftest-patch.py <путь к openapi.yaml> <режим>
"""

from __future__ import annotations

import pathlib
import sys

BLOCK = """        user_sections:
          type: array
          description: Секции уровня пользователя.
          items:
            $ref: '#/components/schemas/SettingsSection'
"""

FREE_KEY_BLOCK = """        user_sections:
          type: object
          description: Свободный словарь «ключ клиента → значение».
          additionalProperties:
            type: string
"""

RENAMES = {
    "caps": "        user_ID_sections:",
    "hyphen": "        'user-sections':",
    "digit": "        '1sections':",
}


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2

    path = pathlib.Path(sys.argv[1])
    mode = sys.argv[2]
    text = path.read_text(encoding="utf-8")

    if BLOCK not in text:
        print(
            f"error: в {path} не найден блок user_sections — патч устарел",
            file=sys.stderr,
        )
        return 2

    if mode == "freekey":
        patched = text.replace(BLOCK, FREE_KEY_BLOCK, 1)
    elif mode in RENAMES:
        patched = text.replace(
            BLOCK, BLOCK.replace("        user_sections:", RENAMES[mode], 1), 1
        )
    else:
        print(f"error: неизвестный режим {mode}", file=sys.stderr)
        return 2

    path.write_text(patched, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
