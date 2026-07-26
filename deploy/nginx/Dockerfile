FROM nginx:1.28

# Привязывает пакет в GHCR к репозиторию: без метки пакет висит отдельно
# и не наследует его видимость.
LABEL org.opencontainers.image.source="https://github.com/gently-whitesnow/bugget"

COPY nginx.conf /etc/nginx/nginx.conf
COPY snippets /etc/nginx/snippets
