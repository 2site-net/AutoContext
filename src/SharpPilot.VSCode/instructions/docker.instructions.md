---
description: "Use when writing, reviewing, or troubleshooting Dockerfiles, Compose files, container builds, or container deployment configuration."
applyTo: "**/Dockerfile*,**/docker-compose*.{yml,yaml},**/.dockerignore"
---
# Docker & Containers Guidelines

## Image Building

- [INST0001] **Do** use multi-stage builds — compile/publish in a build stage and copy only the output to a minimal runtime image.
- [INST0002] **Do** pin base image tags to a specific major/minor version (e.g., `mcr.microsoft.com/dotnet/aspnet:9.0`) — never use `latest` in production.
- [INST0003] **Do** order layers from least-frequently to most-frequently changing — dependency restore before source copy to maximise layer caching.
- [INST0004] **Do** include a `.dockerignore` file that excludes `bin/`, `obj/`, `node_modules/`, `.git/`, and other build artifacts.
- [INST0005] **Don't** install tools or packages in the runtime image that are only needed during build — keeps the image small and reduces attack surface.

## Runtime & Security

- [INST0006] **Do** run the container process as a non-root user — add `USER app` or equivalent after setting up the filesystem.
- [INST0007] **Do** define a `HEALTHCHECK` instruction so orchestrators can detect unhealthy containers and restart them.
- [INST0008] **Do** use environment variables or mounted secrets for configuration — never bake secrets, connection strings, or credentials into the image.
- [INST0009] **Don't** use `--privileged` or add unnecessary Linux capabilities — follow the principle of least privilege.
- [INST0010] **Don't** expose ports that the application doesn't need — declare only the required `EXPOSE` ports.

## Compose

- [INST0011] **Do** use named volumes for persistent data (databases, caches) — bind mounts are for development only.
- [INST0012] **Do** set `restart: unless-stopped` or `restart: on-failure` for services that should survive host reboots.
- [INST0013] **Do** define explicit `depends_on` with `condition: service_healthy` when a service requires another to be ready.
- [INST0014] **Don't** hard-code environment values in the Compose file — use `.env` files or variable substitution and keep `.env` out of source control.
