---
description: "Use when writing, reviewing, or troubleshooting Dockerfiles, Compose files, container builds, or container deployment configuration."
applyTo: "**/Dockerfile*,**/docker-compose*.{yml,yaml},**/.dockerignore"
---
# Docker & Containers Guidelines

## Image Building

- **Do** use multi-stage builds — compile/publish in a build stage and copy only the output to a minimal runtime image.
- **Do** pin base image tags to a specific major/minor version (e.g., `mcr.microsoft.com/dotnet/aspnet:9.0`) — never use `latest` in production.
- **Do** order layers from least-frequently to most-frequently changing — dependency restore before source copy to maximise layer caching.
- **Do** include a `.dockerignore` file that excludes `bin/`, `obj/`, `node_modules/`, `.git/`, and other build artifacts.
- **Don't** install tools or packages in the runtime image that are only needed during build — keeps the image small and reduces attack surface.

## Runtime & Security

- **Do** run the container process as a non-root user — add `USER app` or equivalent after setting up the filesystem.
- **Do** define a `HEALTHCHECK` instruction so orchestrators can detect unhealthy containers and restart them.
- **Do** use environment variables or mounted secrets for configuration — never bake secrets, connection strings, or credentials into the image.
- **Don't** use `--privileged` or add unnecessary Linux capabilities — follow the principle of least privilege.
- **Don't** expose ports that the application doesn't need — declare only the required `EXPOSE` ports.

## Compose

- **Do** use named volumes for persistent data (databases, caches) — bind mounts are for development only.
- **Do** set `restart: unless-stopped` or `restart: on-failure` for services that should survive host reboots.
- **Do** define explicit `depends_on` with `condition: service_healthy` when a service requires another to be ready.
- **Don't** hard-code environment values in the Compose file — use `.env` files or variable substitution and keep `.env` out of source control.
