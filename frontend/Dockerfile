FROM oven/bun:1-alpine AS build
WORKDIR /app
COPY package.json bun.lock ./
RUN bun install --frozen-lockfile
COPY . .
# Default kosong = FE memanggil origin-nya sendiri, diteruskan nginx ke backend (lihat nginx.conf.template).
# API di domain lain: --build-arg VITE_API_URL=https://api.example.com
ARG VITE_API_URL=
ENV VITE_API_URL=$VITE_API_URL
RUN bun run build

FROM nginx:alpine
# Wajib sama dengan nama service + port backend; template dirender ulang tiap container start
ENV BACKEND_URL=http://chess-api:5004
# Tanpa ini, env var senama var nginx (mis. -e host=...) ikut ketimpa saat render
ENV NGINX_ENVSUBST_FILTER=BACKEND_URL
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
