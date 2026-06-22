# Creates the Stripe Products/Prices for:
#   - the 3 add-ons (AI Bundle, Excel Export, Donation Statements)
#   - the Multi-Iglesia network plan (base + per-increment block)
#
# Requires the Stripe CLI (https://stripe.com/docs/stripe-cli) installed and
# logged in to the account/mode you want (test or live):
#   stripe login
# Then run from PowerShell:
#   .\scripts\create-stripe-prices-addons-network.ps1
#
# Prints the STRIPE_PRICE_* env vars to paste into your config at the end.

$ErrorActionPreference = "Stop"

function New-StripePrice {
    param(
        [string]$ProductName,
        [int]$AmountCents,
        [string]$LookupKey
    )

    $productJson = stripe products create `
        --name "$ProductName" `
        -d "metadata[app]=CFS" `
        -o json
    $productId = ($productJson | ConvertFrom-Json).id

    $priceJson = stripe prices create `
        --product "$productId" `
        --currency usd `
        --unit-amount "$AmountCents" `
        -d "recurring[interval]=month" `
        -d "lookup_key=$LookupKey" `
        -o json
    $priceId = ($priceJson | ConvertFrom-Json).id

    return $priceId
}

Write-Host "Creating add-on prices..."
$addonAi = New-StripePrice -ProductName "CFS Add-on: Bundle IA completo" -AmountCents 3500 -LookupKey "addon_ai_bundle_monthly"
$addonExcel = New-StripePrice -ProductName "CFS Add-on: Integracion Excel Automatizado" -AmountCents 1500 -LookupKey "addon_excel_monthly"
$addonDonations = New-StripePrice -ProductName "CFS Add-on: Declaracion de Donativos" -AmountCents 1500 -LookupKey "addon_donations_monthly"

Write-Host "Creating Multi-Iglesia network prices..."
$networkBase = New-StripePrice -ProductName "CFS Multi-Iglesia: Base (hasta 10 iglesias)" -AmountCents 25000 -LookupKey "multichurch_base_monthly"
$networkIncrement = New-StripePrice -ProductName "CFS Multi-Iglesia: Bloque adicional (5 iglesias)" -AmountCents 12500 -LookupKey "multichurch_increment_monthly"

Write-Host ""
Write-Host "Done. Add these to your config (env vars / secrets):"
Write-Host "----------------------------------------------------"
Write-Host "STRIPE_PRICE_ADDON_AI_BUNDLE=$addonAi"
Write-Host "STRIPE_PRICE_ADDON_EXCEL=$addonExcel"
Write-Host "STRIPE_PRICE_ADDON_DONATIONS=$addonDonations"
Write-Host "STRIPE_PRICE_MULTICHURCH_BASE=$networkBase"
Write-Host "STRIPE_PRICE_MULTICHURCH_INCREMENT=$networkIncrement"
