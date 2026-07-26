#!/bin/sh
set -e

# Заменяем плейсхолдеры в env.js на реальные значения
sed -i "s|__BASE_PATH__|${BASE_PATH:-/}|g" /usr/share/nginx/html/env.js
sed -i "s|__USERS_API_URL__|${USERS_API_URL}|g" /usr/share/nginx/html/env.js
sed -i "s|__AUTH_TYPE__|${AUTH_TYPE}|g" /usr/share/nginx/html/env.js
sed -i "s|__MATTERMOST_USER_ID_REQUIRED__|${MATTERMOST_USER_ID_REQUIRED}|g" /usr/share/nginx/html/env.js
sed -i "s|__USER_NAME_REQUIRED__|${USER_NAME_REQUIRED}|g" /usr/share/nginx/html/env.js
sed -i "s|__MATTERMOST_BOT_DM_URL__|${MATTERMOST_BOT_DM_URL}|g" /usr/share/nginx/html/env.js

# Запускаем Nginx
exec nginx -g "daemon off;"
