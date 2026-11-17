# Frontend do Bank — specs/004-frontend-base (research R9)
# Build com Node e entrega estática pelo nginx, que também encaminha /api e /otlp (mesma origem).

FROM node:26.13.0-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
ARG VITE_TELEMETRIA_HABILITADA=false
ARG VITE_OTLP_URL=/otlp
ENV VITE_TELEMETRIA_HABILITADA=$VITE_TELEMETRIA_HABILITADA \
    VITE_OTLP_URL=$VITE_OTLP_URL
RUN npm run build

FROM nginx:1.30.6-alpine
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
