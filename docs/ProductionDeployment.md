# Production deployment

This deployment runs the full stack in production mode:

- React client served by Nginx inside a container.
- .NET API served on port `8080` inside a container.
- Meter AI FastAPI service on the internal Docker network.
- PostgreSQL 16 with a persistent Docker volume.
- Caddy reverse proxy on ports `80` and `443`.

## One-time EC2 setup

Install Docker and the Compose plugin on an Ubuntu EC2 instance, then create the deploy directory:

```bash
sudo mkdir -p /opt/smart-rental-platform
sudo chown "$USER:$USER" /opt/smart-rental-platform
```

Open inbound ports `80` and `443`. Keep PostgreSQL internal; do not expose port `5432` publicly.

## GitHub settings

Create these repository secrets:

- `EC2_HOST`
- `EC2_USER`
- `EC2_SSH_KEY`
- `EC2_DEPLOY_PATH`, for example `/opt/smart-rental-platform`
- `GHCR_PAT`, only if the EC2 server cannot pull private GHCR images with the default package permissions

Create these repository variables:

- `PRODUCTION_SITE_URL`, for example `https://your-domain.com`
- `VITE_GOOGLE_CLIENT_ID`
- `VITE_VIETMAP_API_KEY`
- `VITE_VIETMAP_TILE_STYLE_URL`
- `VITE_LEAFLET_TILE_URL`

## First deployment

Run the `Production CI/CD` workflow manually once. The first deploy creates `.env.production` on the EC2 server from `env.production.template` and stops.

SSH into the server and fill real values:

```bash
cd /opt/smart-rental-platform
nano .env.production
```

Important values:

- `SITE_ADDRESS`: use `:80` for IP-only demo, or `your-domain.com` for automatic HTTPS.
- `PUBLIC_SITE_URL`: must match the browser URL and API CORS origin.
- `POSTGRES_PASSWORD` and `CONNECTIONSTRINGS__DEFAULTCONNECTION`: keep the same database credentials.
- `JWT__SECRETKEY`: at least 32 random characters.
- `AWS__S3__*`: required because the API validates S3 config at startup.
- `ROBOFLOW_API_KEY`: required for meter reading AI.

Rerun the workflow. It will pull new images, run EF Core migrations with `/app/efbundle`, restart the stack, and call `/api/health`.

## Normal deployment flow

After setup, merge or push to `main`:

1. GitHub Actions builds and tests backend and frontend.
2. Docker images are pushed to GHCR.
3. EC2 pulls the new images.
4. Database migrations run before API restart.
5. The production stack restarts through Docker Compose.

## Useful server commands

```bash
cd /opt/smart-rental-platform
docker compose --env-file .env.production -f docker-compose.prod.yml ps
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f api
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f caddy
docker compose --env-file .env.production -f docker-compose.prod.yml run --rm api-migrate
```
