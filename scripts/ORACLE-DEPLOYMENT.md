# Oracle Cloud Deployment

> **Canonical version:** [`docs/deployment/oracle-cloud.md`](../docs/deployment/oracle-cloud.md). That document is the contributor handoff — start there. The runbook below predates the consolidation and is kept as a script-adjacent quick reference.

Single-command deployment of Victoria-like to Oracle Cloud's free tier (4-core ARM VM, $0/mo).

## Prerequisites

1. **OCI CLI installed and configured:**
   ```bash
   brew install oci-cli
   oci setup config
   ```

2. **Your public key uploaded to Oracle:**
   - OCI Console → Identity → Users → your user → API keys → Add API Key
   - Paste `~/.oci/oci_api_key_public.pem` and save

3. **SSH key in place:**
   ```bash
   ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519 (if you don't have one)
   ```

## Deploy

```bash
# From the vic2 repo root:
chmod +x scripts/deploy-oracle.sh
OCI_REGION=us-phoenix-1 scripts/deploy-oracle.sh
```

Change `us-phoenix-1` to your preferred region if needed. ARM capacity varies by region — Phoenix, Frankfurt, and London are usually good.

The script will:
1. Create a 4-core ARM VM with 24GB RAM
2. Open HTTP/HTTPS/SSH in the security list
3. Install Postgres, Redis, .NET 10, and Caddy
4. Output your public IP and next steps

## After Deployment

**SSH into the VM:**
```bash
ssh ubuntu@<public-ip-from-script-output>
```

**Deploy the server:**
```bash
git clone https://github.com/GideonPotok/victoria-like.git ~/victoria-like
cd ~/victoria-like
dotnet publish server/src/VictoriaLike.Server/VictoriaLike.Server.csproj \
  -c Release -r linux-arm64 --self-contained -o ~/victoria-server
```

**Run migrations:**
```bash
# From the vic2 directory
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__DefaultConnection="Host=localhost;Database=victoria_world;Username=victoria;Password=victoria_prod_password;Maximum Pool Size=100" \
  dotnet ef database update --project server/src/VictoriaLike.Server
```

**Set up the systemd unit:**
```bash
scp scripts/setup-systemd-unit.sh ubuntu@<ip>:/tmp/
ssh ubuntu@<ip> sudo bash /tmp/setup-systemd-unit.sh
```

**Configure Caddy:**
```bash
# On your laptop, edit Caddyfile with your domain
cp scripts/Caddyfile.template Caddyfile
# Replace DOMAIN with your actual domain (e.g., victoria.example.com)
# Point your domain's DNS A record to the VM's public IP

scp Caddyfile ubuntu@<ip>:/tmp/
ssh ubuntu@<ip> sudo cp /tmp/Caddyfile /etc/caddy/Caddyfile
ssh ubuntu@<ip> sudo systemctl reload caddy
```

**Start the server:**
```bash
ssh ubuntu@<ip> sudo systemctl start victoria
ssh ubuntu@<ip> sudo systemctl status victoria
ssh ubuntu@<ip> journalctl -u victoria -f  # Watch logs
```

Your server is now live at `https://your-domain.com` with WebSocket support at `wss://your-domain.com/ws`.

## Costs

**$0/month** indefinitely — you're using Oracle's free tier.

- 4 ARM cores + 24GB RAM ✓ free
- 100GB persistent storage ✓ free
- Up to 10TB egress/month ✓ free
- Caddy's Let's Encrypt certs ✓ free

Only pay if you exceed 10TB outbound bandwidth per month (very unlikely).

## Troubleshooting

**VM creation fails with "Out of host capacity":**
- ARM capacity is scarce in popular regions. Try: `us-phoenix-1`, `eu-frankfurt-1`, `eu-london-1`
- Or modify `deploy-oracle.sh` to pick a different region

**SSH hangs or times out:**
- Security list rules may not have applied yet. Wait 30s and retry.
- Check `oci compute instance get --instance-id <id> --query 'data."lifecycle-state"'` shows `RUNNING`.

**Postgres or Redis not responding:**
- They only listen on localhost. This is intentional — your .NET server connects locally.

**Caddy can't get a cert:**
- Ensure your DNS A record points to the VM's public IP
- Wait a few minutes for DNS propagation
- Check `sudo systemctl status caddy` and `sudo journalctl -u caddy -f` for errors

**Server won't start:**
- Check `sudo systemctl status victoria`
- Tail logs: `journalctl -u victoria -f`
- Ensure migrations ran: `psql postgresql://victoria:victoria_prod_password@localhost/victoria_world`

## Cleanup

To delete everything and stop paying (though you're already free):
```bash
oci compute instance terminate --instance-id <id> --force
```

Find your instance ID from the deployment output or:
```bash
oci compute instance list --compartment-id <compartment-id> --query 'data[?display_name==`victoria-server`].id' --raw-output
```
