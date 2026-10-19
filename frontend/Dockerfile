# Frontend: Vite + React static build served by nginx. Build from repo root:
#   docker build -f frontend/Dockerfile --build-arg VITE_API_URL=https://fmlx-chess-api.rizalanggoro.my.id -t rizalanggoro/fmlx-chess:frontend .
ARG VITE_API_URL=http://localhost:5004
FROM oven/bun:1-alpine AS build
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
WORKDIR /app
COPY frontend/package.json frontend/bun.lock ./
RUN bun install --frozen-lockfile
COPY frontend/ ./
RUN bun run build

FROM nginx:alpine
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
