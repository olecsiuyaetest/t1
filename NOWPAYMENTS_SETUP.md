# NowPayments Integration Setup Guide

This guide will walk you through setting up NowPayments for development and production.

## Prerequisites

- A NowPayments account (sign up at https://nowpayments.io)
- Your application deployed and accessible via HTTPS (required for webhooks)
- PostgreSQL database configured

## Step 1: Create a NowPayments Account

1. Go to https://nowpayments.io and sign up for an account
2. Complete the registration process
3. Verify your email address

## Step 2: Set Up Your NowPayments Account

### 2.1 Add Payout Wallet

1. Log in to your NowPayments dashboard
2. Navigate to **Settings** or **Wallet** section
3. Add your cryptocurrency wallet address where you want to receive payments
4. Select the cryptocurrencies you want to accept (e.g., USDT TRC20, BTC, ETH)

### 2.2 Generate API Key

1. Navigate to **Settings** → **API** or **API Settings**
2. Click **Generate API Key** or **Create API Key**
3. **Important**: Copy and save your API key immediately - you won't be able to see it again
4. Store it securely (you'll need it for configuration)

### 2.3 Generate IPN Secret Key

1. Navigate to **Settings** → **Store Settings** or **IPN Settings**
2. Find the **IPN Secret Key** section
3. Click **Generate** or **Create** to generate a new IPN secret key
4. **Important**: Copy and save this secret key - you'll need it for webhook signature verification

### 2.4 Configure IPN Callback URL

1. In the same **Store Settings** or **IPN Settings** section
2. Set the **IPN Callback URL** to: `https://your-domain.com/api/payment/webhook`
   - Replace `your-domain.com` with your actual domain
   - The path `/api/payment/webhook` is the webhook endpoint in your application

## Step 3: Configure Your Application

### 3.1 Update appsettings.json

Open `appsettings.json` and configure the `NowPayments` section:

```json
{
  "NowPayments": {
    "ApiKey": "YOUR_API_KEY_HERE",
    "IpnSecretKey": "YOUR_IPN_SECRET_KEY_HERE",
    "BaseUrl": "https://api.nowpayments.io/v1",
    "IpnCallbackUrl": "https://your-domain.com/api/payment/webhook",
    "PriceCurrency": "USD",
    "PriceAmount": 10.0,
    "PayCurrency": "USDTTRC20"
  }
}
```

**Configuration Details:**
- `ApiKey`: Your NowPayments API key from Step 2.2
- `IpnSecretKey`: Your IPN secret key from Step 2.3
- `BaseUrl`: Keep as `https://api.nowpayments.io/v1` for production
- `IpnCallbackUrl`: Your webhook endpoint URL (must be HTTPS)
- `PriceCurrency`: The fiat currency for pricing (e.g., USD, EUR)
- `PriceAmount`: The price amount in the specified currency
- `PayCurrency`: The cryptocurrency to accept (e.g., `USDTTRC20`, `BTC`, `ETH`)

### 3.2 For Development (Sandbox)

For development and testing, you can use NowPayments Sandbox. The sandbox environment allows you to test payments without using real cryptocurrency.

**Sandbox Documentation**: https://documenter.getpostman.com/view/7907941/T1LSCRHC#9573fff3-b68d-450a-b803-cc7709578902

#### Sandbox Setup Steps:

1. **Sign up for Sandbox Account**
   - Go to https://sandbox.nowpayments.io
   - Create a separate account (different from production)
   - Complete registration and verify email

2. **Configure Sandbox Account**
   - Add a test payout wallet (any valid crypto address format will work in sandbox)
   - Generate Sandbox API Key (Settings → API → Generate API Key)
   - Generate Sandbox IPN Secret Key (Settings → Store Settings → IPN Secret Key)
   - Set IPN Callback URL (Settings → Store Settings → IPN Callback URL)

3. **Update `appsettings.json` for Sandbox**:
   ```json
   {
     "NowPayments": {
       "ApiKey": "YOUR_SANDBOX_API_KEY",
       "IpnSecretKey": "YOUR_SANDBOX_IPN_SECRET_KEY",
       "BaseUrl": "https://api-sandbox.nowpayments.io/v1",
       "IpnCallbackUrl": "https://your-dev-domain.com/api/payment/webhook",
       "PriceCurrency": "USD",
       "PriceAmount": 10.0,
       "PayCurrency": "USDTTRC20"
     }
   }
   ```

**Important Sandbox Notes:**
- **Sandbox Base URL**: `https://api-sandbox.nowpayments.io/v1` (different from production)
- **Sandbox Dashboard**: https://sandbox.nowpayments.io/dashboard
- **No Real Payments**: All transactions in sandbox are simulated
- **Test Payment Methods**: In sandbox, you can simulate different payment scenarios (success, failure, etc.)
- **Separate Credentials**: Sandbox uses different API keys and IPN secret keys than production

**For Local Development with ngrok:**

Since NowPayments requires HTTPS for webhooks, you'll need to expose your local server:

```bash
# Install ngrok (if not installed)
# https://ngrok.com/download

# Start your application on localhost:5000 (or your port)
# Example: dotnet run --urls "http://localhost:5000"

# In another terminal, run ngrok:
ngrok http 5000

# Copy the HTTPS URL (e.g., https://abc123.ngrok.io)
# Use this URL in your IpnCallbackUrl:
# "IpnCallbackUrl": "https://abc123.ngrok.io/api/payment/webhook"

# Important: Update the IPN Callback URL in your sandbox dashboard to match!
```

**Alternative: Use appsettings.Development.json**

You can create a separate configuration file for development:

1. Create `appsettings.Development.json`:
   ```json
   {
     "NowPayments": {
       "ApiKey": "YOUR_SANDBOX_API_KEY",
       "IpnSecretKey": "YOUR_SANDBOX_IPN_SECRET_KEY",
       "BaseUrl": "https://api-sandbox.nowpayments.io/v1",
       "IpnCallbackUrl": "https://your-ngrok-url.ngrok.io/api/payment/webhook",
       "PriceCurrency": "USD",
       "PriceAmount": 10.0,
       "PayCurrency": "USDTTRC20"
     }
   }
   ```

2. This file will automatically be used when running with `ASPNETCORE_ENVIRONMENT=Development`

### 3.3 Apply Database Migration

Run the database migration to create the `payments` table:

```bash
cd CryptoTgShop
dotnet ef database update
```

## Step 4: Testing the Integration

### 4.1 Test Payment Creation

1. Start your application
2. In your Telegram bot, select a category
3. You should receive a payment link
4. The payment should be saved in the database
5. Check your database to verify the payment record was created

### 4.2 Test Webhook (Sandbox Recommended)

**Testing in Sandbox:**

1. **Create a test payment** through your bot
2. **Open the payment link** in your browser
3. **In Sandbox**, you can:
   - Use the sandbox dashboard to view payment status
   - Simulate different payment scenarios
   - Manually trigger webhook callbacks for testing (if available in sandbox dashboard)

4. **Check your application logs** to see if the webhook was received:
   - Look for "========== NOWPAYMENTS WEBHOOK START =========="
   - Verify signature verification passed
   - Check payment status updates

5. **Verify the complete flow**:
   - The payment status was updated in the database
   - The item was sent to the user (if payment was successful)
   - The DataRecord was marked as used
   - The user received the photo and message

**Sandbox Testing Tips:**
- Payments in sandbox don't require real cryptocurrency
- You can test failed payments, expired payments, etc.
- Check the sandbox dashboard to see payment status changes
- Review logs to ensure webhook signature verification works correctly

### 4.3 Verify Webhook Signature

The webhook handler automatically verifies the signature. Check logs for:
- "Signature verification passed" (success)
- "Webhook rejected: Signature mismatch" (failure - check your IPN secret key)

## Step 5: Going to Production

1. **Switch API credentials**:
   - Replace sandbox API key with production API key
   - Replace sandbox IPN secret key with production IPN secret key
   - Update `BaseUrl` to `https://api.nowpayments.io/v1`

2. **Update webhook URL**:
   - Ensure `IpnCallbackUrl` points to your production domain
   - Domain must use HTTPS (SSL certificate required)

3. **Test in production**:
   - Create a small test payment
   - Verify webhook is received
   - Confirm item delivery works correctly

4. **Monitor**:
   - Check application logs regularly
   - Monitor payment statuses in database
   - Set up alerts for failed webhooks

## Troubleshooting

### Webhook Not Received

1. **Check HTTPS**: NowPayments requires HTTPS for webhooks
2. **Verify URL**: Ensure the callback URL is correct and accessible
3. **Check Firewall**: Ensure your server accepts POST requests on the webhook endpoint
4. **Check Logs**: Look for webhook requests in your application logs

### Signature Verification Fails

1. **Verify IPN Secret Key**: Ensure it matches the one in NowPayments dashboard
2. **Check Configuration**: Verify `IpnSecretKey` in `appsettings.json`
3. **Check Logs**: Review signature computation logs

### Payment Created But Not Showing in Dashboard

1. **Check API Key**: Verify you're using the correct API key
2. **Check Environment**: Ensure you're checking the correct environment (sandbox vs production)
3. **Check Payment Status**: Some payments may be in "waiting" status initially

### Items Not Delivered After Payment

1. **Check Webhook**: Verify webhook was received (check logs)
2. **Check Payment Status**: Ensure payment status is `Confirmed` or `Finished`
3. **Check Database**: Verify payment record has `DataRecordId` set
4. **Check Logs**: Look for errors in item delivery process

## API Endpoints

### Payment Webhook
- **URL**: `/api/payment/webhook`
- **Method**: POST
- **Authentication**: Signature in `x-nowpayments-sig` header
- **Description**: Receives payment status updates from NowPayments

## Payment Status Flow

1. **Pending**: Payment intent created
2. **Waiting**: Waiting for payment
3. **Confirming**: Payment detected, confirming on blockchain
4. **Confirmed**: Payment confirmed
5. **Sending**: Processing delivery
6. **Finished**: Payment complete, item delivered
7. **Failed**: Payment failed
8. **Refunded**: Payment refunded
9. **Expired**: Payment expired

## Security Notes

1. **Keep API keys secure**: Never commit API keys to version control
2. **Use environment variables**: Consider using `appsettings.Development.json` or environment variables for sensitive data
3. **Verify webhooks**: Always verify webhook signatures before processing
4. **HTTPS only**: Always use HTTPS in production
5. **Rate limiting**: Consider implementing rate limiting on webhook endpoint

## Additional Resources

- **NowPayments Production API Documentation**: https://documenter.getpostman.com/view/7907941/2s93JusNJt
- **NowPayments Sandbox API Documentation**: https://documenter.getpostman.com/view/7907941/T1LSCRHC#9573fff3-b68d-450a-b803-cc7709578902
- **NowPayments Production Dashboard**: https://nowpayments.io/dashboard
- **NowPayments Sandbox Dashboard**: https://sandbox.nowpayments.io/dashboard
- **NowPayments Sandbox**: https://sandbox.nowpayments.io
- **Support**: Contact NowPayments support for account-related issues

**Key Differences Between Sandbox and Production:**

| Feature | Sandbox | Production |
|--------|---------|------------|
| Base URL | `https://api-sandbox.nowpayments.io/v1` | `https://api.nowpayments.io/v1` |
| Dashboard | `https://sandbox.nowpayments.io/dashboard` | `https://nowpayments.io/dashboard` |
| Payments | Simulated, no real crypto | Real cryptocurrency transactions |
| API Keys | Separate sandbox keys | Production keys |
| IPN Secret | Separate sandbox secret | Production secret |

## Next Steps

After setup:
1. Test the integration thoroughly in sandbox
2. Monitor the first few production payments closely
3. Set up logging and monitoring
4. Consider implementing payment status polling as a backup to webhooks
5. Add admin dashboard to view payments and manage orders

