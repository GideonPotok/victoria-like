# Oracle Cloud Free Tier Deployment — Contributor Handoff

This doc consolidates the Oracle Cloud Free Tier deployment plan into a single handoff. It is intentionally structured as "here is the plan, here is what has been done, here is exactly what a contributor needs to do to finish it."

**As of this writing, the deployment has NOT been executed against any real Oracle account.** Scripts and a runbook are checked in and were Opus-reviewed; the provisioning has not happened.

## Why Oracle Free Tier

Oracle Cloud Infrastructure (OCI) Free Tier includes an **always-free** Ampere ARM allotment: up to 4 OCPUs and 24 GB RAM split across instances of `VM.Standard.A1.Flex`. That's enough to run the .NET server, Postgres, Redis, and a separate load-test VM — all at $0/month indefinitely. The non-obvious upside for this project: the load tester and the target can both live in the same datacenter, which makes "prove the system survives N clients" a meaningful claim instead of a homelab bandwidth claim.

Alternatives that were considered and rejected for this stage:

- **Fly.io.** Cleanest deploy story but no longer has a true free tier; you'd pay ~$2-3/mo for the server plus managed Postgres elsewhere (Supabase free).
- **GCP / AWS / Azure free tier.** Credit-card traps, surprise egress bills, and load-balancer defaults hostile to long-lived WebSockets (the GCP HTTPS LB has a 30s backend timeout you have to remember to raise).
- **Supabase / Vercel / Cloud Run.** Wrong shape — this is an always-on fixed-tick simulation, not a request/response service.

If you decide to do Fly.io instead, the rough shape was: shared-cpu-1x + Supabase free Postgres + Upstash free Redis, with the Supabase transaction pooler on port 6543, `abortConnect=False` on Redis, and a 1GB volume mounted at `/data` so snapshots survive deploys. EF Core migrations have to run from the laptop against the **session** pooler (port 5432), not the transaction pooler. That's the whole spike — none of it is committed.

## What's Already In The Repo

Four files in `scripts/`, all reviewed but not executed:

| File | Purpose |
|------|---------|
| [`scripts/deploy-oracle.sh`](../../scripts/deploy-oracle.sh) | Provisions the ARM VM via OCI CLI: picks an Ampere A1 image, cycles ADs on capacity errors, opens 80/443 in the security list, installs Postgres + Redis + Caddy + .NET 10 over SSH, generates and stashes a Postgres password under `~/.victoria-deploy/postgres_password`. |
| [`scripts/setup-systemd-unit.sh`](../../scripts/setup-systemd-unit.sh) | Runs **on the VM**. Writes `/etc/victoria/server.env` (mode 640, root-owned, ubuntu-readable) with the connection strings, then installs `/etc/systemd/system/victoria.service` for the .NET server (auto-restart, journald logging, raised file/process limits). |
| [`scripts/Caddyfile.template`](../../scripts/Caddyfile.template) | Reverse-proxy template for Caddy. Replace `DOMAIN` with the actual hostname, drop into `/etc/caddy/Caddyfile`, reload. WebSocket upgrade works transparently; Caddy handles Let's Encrypt. |
| [`scripts/ORACLE-DEPLOYMENT.md`](../../scripts/ORACLE-DEPLOYMENT.md) | Original runbook. Superseded by this document but retained as a script-adjacent reference. |

The shape of the deployment is one fat VM (4 OCPUs / 24 GB) with Postgres and Redis on localhost, Caddy fronting the .NET server on `127.0.0.1:8080`, and TLS auto-provisioned via Let's Encrypt. There is no managed database, no load balancer, and no horizontal scaling — that is intentional at this scale.

## What's Not Done

Nothing has been provisioned or executed against a real account. Specifically:

- No OCI tenancy / compartment configured.
- No VM exists.
- No DNS A record, no domain.
- No `dotnet publish` artifact has been built for `linux-arm64`.
- No EF Core migrations have been run against a remote Postgres.
- The systemd unit was never installed on a host.
- Caddy never requested a Let's Encrypt cert.
- The Unity client `BaseUrl` has not been pointed at any remote host.

## Picking This Up — Step-By-Step

Each step is independent. If you get stuck, the prerequisite step probably failed silently.

### 1. Prerequisites on your laptop

```bash
brew install oci-cli jq
oci setup config            # walks you through API key + tenancy
ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519   # if you don't already have one
```

Upload your laptop's OCI public key under *OCI Console → Identity → Users → your user → API Keys*. Paste `~/.oci/oci_api_key_public.pem`.

You also need an OCI VCN with at least one public subnet. The OCI Console's *Networking → Quickstart VCN* flow will create both in one click; the script bails out with a clear error if it can't find them.

### 2. Provision the VM

From this repo's root:

```bash
chmod +x scripts/deploy-oracle.sh
OCI_REGION=us-phoenix-1 scripts/deploy-oracle.sh
```

`OCI_REGION` matters. ARM A1 capacity is famously scarce in Ashburn (`us-ashburn-1`) and San Jose (`us-sanjose-1`). Phoenix, Frankfurt (`eu-frankfurt-1`), and London (`eu-london-1`) usually have headroom. The script will cycle all ADs in the chosen region and retry rounds with backoff, but it can't move regions for you — if Phoenix is wedged, kill the script and re-run with a different region.

The script prints the public IP and stashes the generated Postgres password at `~/.victoria-deploy/postgres_password` (mode 600). Hold onto both.

### 3. Open `iptables` if you used a stock Ubuntu image

The Canonical Ubuntu image on OCI ships with an `iptables` REJECT rule that drops anything not 22/68/etc. The script handles this for ports 80 and 443 by inserting before the REJECT and persisting via `iptables-persistent`. If the script bailed early, do it manually:

```bash
ssh ubuntu@<vm-ip>
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80  -j ACCEPT
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

This is the single most common reason a fresh OCI VM looks "unreachable" — it's not DNS, it's iptables.

### 4. Push the code, build the artifact

```bash
ssh ubuntu@<vm-ip>
git clone https://github.com/GideonPotok/victoria-like.git ~/victoria-like
cd ~/victoria-like
~/.dotnet/dotnet publish server/src/VictoriaLike.Server/VictoriaLike.Server.csproj \
    -c Release -r linux-arm64 --self-contained -o ~/victoria-server
```

The `--self-contained` flag is important — the systemd unit assumes the binary at `/home/ubuntu/victoria-server/VictoriaLike.Server` runs without a separate .NET install in `PATH`.

### 5. Run EF Core migrations against the local Postgres

```bash
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__DefaultConnection="Host=localhost;Database=victoria_world;Username=victoria;Password=$(cat ~/.victoria-deploy/postgres_password);Maximum Pool Size=100" \
  ~/.dotnet/dotnet ef database update --project server/src/VictoriaLike.Server
```

Run this from the VM, against the localhost Postgres the deploy script already created. The role and database are named `victoria` / `victoria_world` to match local dev.

### 6. Install the systemd unit

From the VM:

```bash
PG_PASS="$(cat ~/.victoria-deploy/postgres_password)" \
  bash ~/victoria-like/scripts/setup-systemd-unit.sh
sudo systemctl start victoria
sudo systemctl status victoria
journalctl -u victoria -f
```

You should see Serilog JSON tick logs every second.

### 7. Point a domain at the VM and turn on Caddy

Pick any DNS provider (DuckDNS works free; Cloudflare is fine). Create an A record pointing your hostname at the VM's public IP. Then:

```bash
# On your laptop
cp scripts/Caddyfile.template Caddyfile
# Edit Caddyfile: replace DOMAIN with your hostname
scp Caddyfile ubuntu@<vm-ip>:/tmp/
ssh ubuntu@<vm-ip> sudo cp /tmp/Caddyfile /etc/caddy/Caddyfile
ssh ubuntu@<vm-ip> sudo systemctl reload caddy
ssh ubuntu@<vm-ip> sudo journalctl -u caddy -f   # watch the cert provisioning
```

Caddy will pull a Let's Encrypt cert automatically. WebSocket upgrades work without any extra config — Caddy detects the `Upgrade` header. Your client connects to `https://<hostname>/` and `wss://<hostname>/ws`.

### 8. Smoke test

```bash
curl https://<hostname>/health
curl https://<hostname>/api/world/countries
```

Expected: a `Healthy` health check and the demo country list. If you used the same scenarios that ship in this repo (which `make run-albion` loads locally), you'll see `Albion`.

## Known Risks And Things To Double-Check

- **`deploy-oracle.sh` was generated by Haiku, then reviewed by Opus.** The review found and fixed a couple of issues (preserving existing security-list rules, dynamic AD discovery, password generation outside the repo, dynamic iptables REJECT-rule line lookup). Re-read it once more before running. The script is also single-shot — it doesn't tear down on partial failure, so a half-failed run leaves orphaned resources you'll have to delete via the OCI console.
- **Connection-string secret handling.** `setup-systemd-unit.sh` writes `/etc/victoria/server.env` with mode 640 / `root:ubuntu` ownership. The systemd unit reads it via `EnvironmentFile=`. The password never appears in the unit file itself, but it does appear in environment variables of the running process — anyone with shell on the VM can `cat /proc/<pid>/environ`. That's acceptable for a single-host hobby deploy and not acceptable for a real production deploy.
- **Backups.** None are configured. A nightly `pg_dump | aws s3 cp -` to Oracle Object Storage (10 GB free) would close the obvious gap; not in scope for the initial provision.
- **Snapshots directory.** The server writes JSON snapshots to `bin/Debug/net10.0/snapshots/` by default. In production, set `World__Snapshots__Directory=/var/lib/victoria/snapshots` (or similar) via the EnvironmentFile and `chown` it to `ubuntu`. Otherwise restart recovery will look for snapshots in the wrong place.
- **`Server__Port`.** The systemd unit pins `ASPNETCORE_URLS=http://127.0.0.1:8080`. The local dev server uses port 5001 (set via `appsettings.json`). Don't confuse the two when reading client config or load-test invocations.
- **OCI account lockout.** Oracle's signup is notoriously hostile to virtual / prepaid cards. Use a real personal credit card and a residential address. If it fails, don't retry immediately — the anti-fraud system gets harder, not softer, on repeated attempts.

## Tear Down

If you want to cleanly remove everything (free tier means there's nothing to bill, but resource hygiene is still nice):

```bash
oci compute instance list \
  --compartment-id "$(awk -F'=' '/^tenancy=/{print $2; exit}' ~/.oci/config)" \
  --query 'data[?display_name==`victoria-server`].id' --raw-output \
  | xargs -I{} oci compute instance terminate --instance-id {} --force
```

Then remove the manually-added 80/443 ingress rules from the security list (the script merges; tear-down does not split). Delete the local password file at `~/.victoria-deploy/postgres_password` if you don't plan to reuse it.

## TL;DR For Future You

1. Read `scripts/deploy-oracle.sh` once.
2. `OCI_REGION=us-phoenix-1 scripts/deploy-oracle.sh`.
3. SSH in, publish, migrate, `setup-systemd-unit.sh`, start the service.
4. Edit Caddyfile, copy, reload.
5. `curl https://<host>/health`. Done.

Total wall-clock time if no capacity hiccups: ~30 minutes. If Phoenix has no capacity and you have to wait an hour to try Frankfurt: still under a day.
