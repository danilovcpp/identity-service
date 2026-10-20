FROM node:22-alpine@sha256:0a7108bf6c7bf5de370ffb1a3ed6be93d405b43ff159f681a8d18c0e2bc2e402 AS build
WORKDIR /source
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

FROM nginx:stable-alpine@sha256:970364228b1e4f932abc22d321932816782cca8a20b85a77552a7cfe9acfb3b5
ARG VCS_REF=unknown
LABEL org.opencontainers.image.title="Identity Console" \
      org.opencontainers.image.version="1.1.0" \
      org.opencontainers.image.revision=$VCS_REF
COPY nginx/ /etc/nginx/
COPY --from=build /source/dist /usr/share/nginx/html
USER 101:101
EXPOSE 8443
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 CMD wget -q -O /dev/null http://127.0.0.1:8081/health/live || exit 1
ENTRYPOINT ["nginx", "-g", "daemon off;"]
