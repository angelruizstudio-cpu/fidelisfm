#!/usr/bin/env bash
# Creates the Stripe Products/Prices for:
#   - the 3 add-ons (AI Bundle, Excel Export, Donation Statements)
#   - the Multi-Iglesia network plan (base + per-increment block)
#
# Run with the Stripe CLI logged into the account/mode you want (test or live):
#   stripe login
# Then:
#   ./scripts/create-stripe-prices-addons-network.sh
#
# Prints the STRIPE_PRICE_* env vars to paste into your config at the end.
# Safe to re-run individual lines if one step fails — Stripe IDs are unique per run.

set -euo pipefail

create_price() {
    local product_name="$1"
    local amount_cents="$2"
    local lookup_key="$3"

    local product_id
    product_id=$(stripe products create \
        --name "$product_name" \
        -d "metadata[app]=CFS" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')

    local price_id
    price_id=$(stripe prices create \
        --product "$product_id" \
        --currency usd \
        --unit-amount "$amount_cents" \
        -d "recurring[interval]=month" \
        -d "lookup_key=$lookup_key" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')

    echo "$lookup_key=$price_id"
}

echo "Creating add-on prices..."
ADDON_AI=$(create_price "CFS Add-on: Bundle IA completo" 3500 "addon_ai_bundle_monthly")
ADDON_EXCEL=$(create_price "CFS Add-on: Integracion Excel Automatizado" 1500 "addon_excel_monthly")
ADDON_DONATIONS=$(create_price "CFS Add-on: Declaracion de Donativos" 1500 "addon_donations_monthly")

echo "Creating Multi-Iglesia network prices..."
NETWORK_BASE=$(create_price "CFS Multi-Iglesia: Base (hasta 10 iglesias)" 25000 "multichurch_base_monthly")
NETWORK_INCREMENT=$(create_price "CFS Multi-Iglesia: Bloque adicional (5 iglesias)" 12500 "multichurch_increment_monthly")

echo
echo "Done. Add these to your config (env vars / secrets):"
echo "----------------------------------------------------"
echo "STRIPE_PRICE_ADDON_AI_BUNDLE=$(echo "$ADDON_AI" | cut -d= -f2)"
echo "STRIPE_PRICE_ADDON_EXCEL=$(echo "$ADDON_EXCEL" | cut -d= -f2)"
echo "STRIPE_PRICE_ADDON_DONATIONS=$(echo "$ADDON_DONATIONS" | cut -d= -f2)"
echo "STRIPE_PRICE_MULTICHURCH_BASE=$(echo "$NETWORK_BASE" | cut -d= -f2)"
echo "STRIPE_PRICE_MULTICHURCH_INCREMENT=$(echo "$NETWORK_INCREMENT" | cut -d= -f2)"
