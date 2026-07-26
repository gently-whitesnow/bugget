FROM node:22-alpine AS builder

# Стабилизируем установку npm-зависимостей на сетях с проблемным IPv6/потерями.
ENV NODE_OPTIONS=--dns-result-order=ipv4first \
    npm_config_registry=https://registry.npmjs.org/ \
    npm_config_fetch_retries=8 \
    npm_config_fetch_retry_mintimeout=20000 \
    npm_config_fetch_retry_maxtimeout=180000 \
    npm_config_fetch_timeout=300000 \
    npm_config_maxsockets=1 \
    npm_config_audit=false \
    npm_config_fund=false \
    npm_config_progress=false

WORKDIR /app
COPY package.json package-lock.json ./
RUN set -eux; \
    for i in 1 2 3; do \
      npm ci && break; \
      rc=$?; \
      if [ "$i" -eq 3 ]; then exit "$rc"; fi; \
      echo "npm ci failed (attempt $i), retrying in 20s"; \
      sleep 20; \
    done
COPY . .
RUN npm run build

# Stage 2: Serve with Nginx
FROM nginx:stable-alpine
WORKDIR /usr/share/nginx/html
COPY --from=builder /app/dist .

# Копируем шаблон и скрипт для runtime переменных
COPY public/env.template.js ./env.js
COPY nginx.conf /etc/nginx/nginx.conf
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80
ENTRYPOINT ["/entrypoint.sh"]
