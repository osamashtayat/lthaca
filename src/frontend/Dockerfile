# syntax=docker/dockerfile:1
# Build context: the repository root (docker compose sets it): the app imports contracts/realtime-events.json.
# Serves the SPA with nginx and proxies /api and /hubs to the API, so the browser sees one origin (no CORS).
FROM node:22-alpine AS build
WORKDIR /repo/src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY contracts/ /repo/contracts/
COPY src/frontend/ ./
# Error tracking (ADR-0028): baked into the bundle at build time; empty = off.
ARG VITE_SENTRY_DSN=""
ARG VITE_SENTRY_RELEASE=""
ENV VITE_SENTRY_DSN=$VITE_SENTRY_DSN VITE_SENTRY_RELEASE=$VITE_SENTRY_RELEASE
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS web
COPY src/frontend/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /repo/src/frontend/dist /usr/share/nginx/html
EXPOSE 8080
