#!/bin/bash
set -euo pipefail

# Oracle Cloud free tier deployment for Victoria-like server.
# Provisions a 4-OCPU / 24GB ARM VM (Ampere A1) with Postgres, Redis, and Caddy.
#
# Required: oci CLI configured (`oci setup config`), jq, ssh key at SSH_KEY.
# Optional env:
#   OCI_REGION           override the region from ~/.oci/config
#   OCI_COMPARTMENT_ID   target compartment (defaults to tenancy / root)
#   SSH_KEY              public key path (default ~/.ssh/id_ed25519.pub)
#   AD_INDEX             which availability domain to try first (default 1)
#   DISPLAY_NAME         instance display name (default victoria-server)

SSH_KEY="${SSH_KEY:-$HOME/.ssh/id_ed25519.pub}"
AD_INDEX="${AD_INDEX:-1}"
DISPLAY_NAME="${DISPLAY_NAME:-victoria-server}"
SECRETS_DIR="${SECRETS_DIR:-$HOME/.victoria-deploy}"

command -v oci >/dev/null || { echo "oci CLI not found. Install: https://docs.oracle.com/iaas/Content/API/SDKDocs/cliinstall.htm" >&2; exit 1; }
command -v jq  >/dev/null || { echo "jq not found. brew install jq" >&2; exit 1; }
[ -f "$SSH_KEY" ] || { echo "SSH public key not found at $SSH_KEY" >&2; exit 1; }

mkdir -p "$SECRETS_DIR"
chmod 700 "$SECRETS_DIR"

echo "=== Oracle Cloud Deployment for Victoria-like ==="

# --- Resolve tenancy / compartment / region from CLI config -----------------
TENANCY_ID="$(oci iam compartment list --all --query 'data[0]."compartment-id"' --raw-output 2>/dev/null || true)"
if [ -z "$TENANCY_ID" ] || [ "$TENANCY_ID" = "null" ]; then
  # Fall back to reading config file directly
  TENANCY_ID="$(awk -F'=' '/^tenancy=/{print $2; exit}' "${OCI_CONFIG_FILE:-$HOME/.oci/config}")"
fi
[ -n "$TENANCY_ID" ] || { echo "ERROR: could not determine tenancy OCID" >&2; exit 1; }

COMPARTMENT_ID="${OCI_COMPARTMENT_ID:-$TENANCY_ID}"
REGION="${OCI_REGION:-$(awk -F'=' '/^region=/{print $2; exit}' "${OCI_CONFIG_FILE:-$HOME/.oci/config}")}"
echo "Tenancy:     $TENANCY_ID"
echo "Compartment: $COMPARTMENT_ID"
echo "Region:      $REGION"

# --- Availability domain (discover, don't fabricate the name) ---------------
AD_NAME="$(oci iam availability-domain list \
  --compartment-id "$TENANCY_ID" \
  --query "data[$((AD_INDEX - 1))].name" --raw-output)"
[ -n "$AD_NAME" ] && [ "$AD_NAME" != "null" ] || { echo "ERROR: no AD found at index $AD_INDEX" >&2; exit 1; }
echo "AD:          $AD_NAME"

# --- VCN + subnet + security list ------------------------------------------
VCN_ID="$(oci network vcn list --compartment-id "$COMPARTMENT_ID" --query 'data[0].id' --raw-output)"
if [ -z "$VCN_ID" ] || [ "$VCN_ID" = "null" ]; then
  echo "ERROR: no VCN in compartment. Create one in the OCI console (Networking → Quickstart VCN) first." >&2
  exit 1
fi
SUBNET_ID="$(oci network subnet list --compartment-id "$COMPARTMENT_ID" --vcn-id "$VCN_ID" --query 'data[0].id' --raw-output)"
if [ -z "$SUBNET_ID" ] || [ "$SUBNET_ID" = "null" ]; then
  echo "ERROR: VCN has no subnets. In the OCI console: Networking → VCNs → vic-van-winkle → Create Subnet (regional, public)." >&2
  exit 1
fi
SECURITY_LIST_ID="$(oci network security-list list --compartment-id "$COMPARTMENT_ID" --vcn-id "$VCN_ID" --query 'data[0].id' --raw-output)"
echo "VCN:         $VCN_ID"
echo "Subnet:      $SUBNET_ID"
echo "SecList:     $SECURITY_LIST_ID"

# --- Image: latest Ubuntu 22.04 aarch64 ------------------------------------
IMAGE_ID="$(oci compute image list \
  --compartment-id "$COMPARTMENT_ID" \
  --operating-system "Canonical Ubuntu" \
  --operating-system-version "22.04" \
  --shape VM.Standard.A1.Flex \
  --sort-by TIMECREATED --sort-order DESC \
  --query 'data[0].id' --raw-output)"
[ -n "$IMAGE_ID" ] && [ "$IMAGE_ID" != "null" ] || { echo "ERROR: no Ubuntu 22.04 ARM image found" >&2; exit 1; }
echo "Image:       $IMAGE_ID"

# --- Generate Postgres password (kept locally, not committed) --------------
PG_PASS_FILE="$SECRETS_DIR/postgres_password"
if [ ! -f "$PG_PASS_FILE" ]; then
  openssl rand -base64 32 | tr -d '\n=+/' | cut -c1-32 > "$PG_PASS_FILE"
  chmod 600 "$PG_PASS_FILE"
  echo "Generated Postgres password → $PG_PASS_FILE"
fi
PG_PASS="$(cat "$PG_PASS_FILE")"

# --- Launch the instance, cycling ADs and retrying transient errors ---------
# Ashburn has 3 ADs; capacity is allocated per-AD so cycling all three before
# sleeping dramatically improves hit rate.
echo
echo "=== Launching VM.Standard.A1.Flex (4 OCPU / 24 GB) ==="

# Build list of all ADs in this region
ALL_ADS="$(oci iam availability-domain list --compartment-id "$TENANCY_ID" --query 'data[*].name' --raw-output | tr ',' '\n' | tr -d '[]" ')"
[ -n "$ALL_ADS" ] || ALL_ADS="$AD_NAME"

LAUNCH_OUT=""
rc=1
for round in 1 2 3 4 5; do
  while IFS= read -r try_ad; do
    [ -n "$try_ad" ] || continue
    echo "  Attempt $round: trying AD $try_ad ..."
    set +e
    LAUNCH_OUT="$(oci compute instance launch \
      --availability-domain "$try_ad" \
      --compartment-id "$COMPARTMENT_ID" \
      --image-id "$IMAGE_ID" \
      --shape VM.Standard.A1.Flex \
      --shape-config '{"ocpus": 4, "memoryInGBs": 24}' \
      --subnet-id "$SUBNET_ID" \
      --ssh-authorized-keys-file "$SSH_KEY" \
      --display-name "$DISPLAY_NAME" \
      --assign-public-ip true \
      --wait-for-state RUNNING \
      --output json 2>&1)"
    rc=$?
    set -e
    if [ $rc -eq 0 ]; then break 2; fi
    if echo "$LAUNCH_OUT" | grep -qi "out of host capacity\|OutOfCapacity\|InternalError\|timed out\|connection.*timeout"; then
      echo "    → no capacity or transient error, trying next AD."
      continue
    fi
    # Any other error is fatal (bad auth, bad image ID, etc.)
    echo "$LAUNCH_OUT" >&2
    exit 1
  done <<< "$ALL_ADS"
  echo "  All ADs tried in round $round. Sleeping 60s before next round."
  sleep 60
done
[ $rc -eq 0 ] || { echo "ERROR: still out of capacity after 5 rounds across all ADs. Try a different region (Phoenix / Frankfurt / London usually have more)." >&2; exit 1; }

VM_ID="$(echo "$LAUNCH_OUT" | jq -r '.data.id')"
echo "✓ VM running: $VM_ID"

# Public IP must come from the VNIC, not from instance launch output.
VNIC_ID="$(oci compute instance list-vnics --instance-id "$VM_ID" --query 'data[0].id' --raw-output)"
VM_IP="$(oci network vnic get --vnic-id "$VNIC_ID" --query 'data."public-ip"' --raw-output)"
[ -n "$VM_IP" ] && [ "$VM_IP" != "null" ] || { echo "ERROR: VM has no public IP" >&2; exit 1; }
echo "✓ Public IP:  $VM_IP"

# --- Add ingress rules (merge, don't replace) ------------------------------
echo
echo "=== Adding ingress rules for 80 / 443 (preserving existing) ==="
EXISTING_RULES="$(oci network security-list get --security-list-id "$SECURITY_LIST_ID" --query 'data."ingress-security-rules"' --output json)"
NEW_RULES="$(echo "$EXISTING_RULES" | jq '
  . + [
    {source:"0.0.0.0/0", protocol:"6", isStateless:false,
     tcpOptions:{destinationPortRange:{min:80,  max:80}}},
    {source:"0.0.0.0/0", protocol:"6", isStateless:false,
     tcpOptions:{destinationPortRange:{min:443, max:443}}}
  ]
  | unique_by(.tcpOptions.destinationPortRange.min // -1, .source, .protocol)
')"
oci network security-list update \
  --security-list-id "$SECURITY_LIST_ID" \
  --ingress-security-rules "$NEW_RULES" \
  --force --output none
echo "✓ Ports 80, 443 open (port 22 left as-is)"

# --- Wait for SSH ----------------------------------------------------------
echo
echo "Waiting for SSH..."
SSH_OPTS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o ConnectTimeout=5"
for i in {1..40}; do
  if ssh $SSH_OPTS ubuntu@"$VM_IP" "true" 2>/dev/null; then
    echo "✓ SSH ready"
    break
  fi
  sleep 5
done

# --- Remote setup ----------------------------------------------------------
REMOTE_SETUP="$(cat <<SETUP
#!/bin/bash
set -euo pipefail

echo "=== Installing iptables-persistent (preseeded to auto-save) ==="
echo iptables-persistent iptables-persistent/autosave_v4 boolean true | sudo debconf-set-selections
echo iptables-persistent iptables-persistent/autosave_v6 boolean true | sudo debconf-set-selections
sudo DEBIAN_FRONTEND=noninteractive apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y iptables-persistent

echo "=== Opening iptables for HTTP/HTTPS ==="
# Insert before the default REJECT rule. Find it dynamically.
REJECT_LINE=\$(sudo iptables -L INPUT --line-numbers | awk '/REJECT/{print \$1; exit}')
INSERT_AT=\${REJECT_LINE:-1}
sudo iptables -I INPUT \$INSERT_AT -p tcp --dport 80  -m state --state NEW,ESTABLISHED -j ACCEPT
sudo iptables -I INPUT \$INSERT_AT -p tcp --dport 443 -m state --state NEW,ESTABLISHED -j ACCEPT
sudo netfilter-persistent save

echo "=== Installing Postgres + Redis ==="
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y postgresql redis-server

echo "=== Installing Caddy ==="
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --batch --yes --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list >/dev/null
sudo apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y caddy

echo "=== Installing .NET 10 SDK ==="
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir \$HOME/.dotnet
grep -q 'HOME/.dotnet' ~/.bashrc || echo 'export PATH=\$PATH:\$HOME/.dotnet' >> ~/.bashrc

echo "=== Configuring Postgres (peer auth still works; setting pw for app user) ==="
sudo -u postgres psql -v ON_ERROR_STOP=1 <<PSQL
DO \\\$\\\$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='victoria') THEN
    CREATE ROLE victoria LOGIN PASSWORD '__PG_PASS__';
  ELSE
    ALTER ROLE victoria WITH PASSWORD '__PG_PASS__';
  END IF;
END \\\$\\\$;
SELECT 'db_exists' FROM pg_database WHERE datname='victoria_world'\\\\gset
PSQL
sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='victoria_world'" | grep -q 1 \\
  || sudo -u postgres createdb -O victoria victoria_world

echo "✓ Remote setup complete."
SETUP
)"

# Inject the password without quoting hell.
REMOTE_SETUP="${REMOTE_SETUP//__PG_PASS__/$PG_PASS}"

echo "$REMOTE_SETUP" | ssh $SSH_OPTS ubuntu@"$VM_IP" "cat > /tmp/victoria-setup.sh && bash /tmp/victoria-setup.sh"

# --- Summary ---------------------------------------------------------------
cat <<EOF

✓ Deploy complete.

=== VM ===
  Instance:  $VM_ID
  Public IP: $VM_IP
  Region:    $REGION / $AD_NAME
  SSH:       ssh ubuntu@$VM_IP

=== Secrets (local, not committed) ===
  Postgres password: $PG_PASS_FILE

=== Next steps ===
  1. ssh ubuntu@$VM_IP
  2. git clone https://github.com/GideonPotok/victoria-like.git ~/victoria-like && cd ~/victoria-like
  3. ~/.dotnet/dotnet publish server/src/VictoriaLike.Server/VictoriaLike.Server.csproj \\
        -c Release -r linux-arm64 --self-contained -o ~/victoria-server
  4. Install systemd unit (uses the generated password):
        PG_PASS="\$(cat $PG_PASS_FILE)" scripts/setup-systemd-unit.sh   # see script
  5. Point a DNS record at $VM_IP, drop it into scripts/Caddyfile.template,
     copy to /etc/caddy/Caddyfile, then: sudo systemctl reload caddy
EOF
