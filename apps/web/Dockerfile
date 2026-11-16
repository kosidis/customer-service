# syntax=docker/dockerfile:1

FROM node:24-alpine AS deps
WORKDIR /app

# Install from the lockfile alone so the dependency layer is cached until
# package.json or package-lock.json changes.
COPY package.json package-lock.json ./
RUN npm ci --loglevel verbose --maxsockets=4 --fetch-retries=5 --fetch-retry-mintimeout=2000

FROM node:24-alpine AS build
WORKDIR /app

ENV NEXT_TELEMETRY_DISABLED=1

COPY --from=deps /app/node_modules ./node_modules
COPY . .
# public/ is optional in the app; the runtime stage copies it unconditionally.
# API_BASE_URL is deliberately not set: the server reads it at runtime.
RUN mkdir -p public && npm run build

FROM node:24-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# Fixed UID/GID 1001 so the chart's securityContext can match it.
RUN addgroup -g 1001 -S nodejs && adduser -S -u 1001 -G nodejs nextjs

# The standalone output holds server.js and the traced node_modules, but not
# static assets or public/, so copy those beside it.
COPY --from=build --chown=1001:1001 /app/.next/standalone ./
COPY --from=build --chown=1001:1001 /app/.next/static ./.next/static
COPY --from=build --chown=1001:1001 /app/public ./public

# Next.js writes its runtime cache here. Pre-create it, owned by 1001, so an
# emptyDir mounted over it works with a read-only root filesystem.
RUN mkdir -p .next/cache && chown 1001:1001 .next/cache

USER 1001
EXPOSE 3000

CMD ["node", "server.js"]
