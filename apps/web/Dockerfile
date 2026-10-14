# syntax=docker/dockerfile:1
# Build from the repository root: docker build -f apps/web/Dockerfile .

FROM node:26-alpine AS build
WORKDIR /src

COPY apps/web/package.json apps/web/package-lock.json apps/web/
RUN npm --prefix apps/web ci

COPY packages/api-contract/openapi.json packages/api-contract/
COPY apps/web/ apps/web/
RUN npm --prefix apps/web run build

FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime

COPY apps/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/apps/web/www /usr/share/nginx/html

USER nginx
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
    CMD wget -qO /dev/null http://127.0.0.1:8080/ || exit 1
