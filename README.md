# DiagnX API

One backend for both DiagnX apps — the **Patient app** and the **Partner (lab) app** — plus an admin back-office.
.NET 8 Web API · PostgreSQL · EF Core · JWT.

## Run it locally

Needs the .NET 8 SDK and PostgreSQL.

1. Create `src/DiagnX.Api/appsettings.Local.json` (git-ignored) with your database password:

   ```json
   {
     "ConnectionStrings": {
       "Default": "Host=localhost;Port=5432;Database=diagnx;Username=postgres;Password=YOUR_PASSWORD"
     }
   }
   ```

2. Start it:

   ```bash
   dotnet run --project src/DiagnX.Api --urls http://localhost:5118
   ```

   The first start creates the `diagnx` database, all tables and the demo data.

3. Open **http://localhost:5118/swagger**. Pick an API group in the "Select a definition" dropdown
   (Patient app / Partner app / Master & files / Admin).

## Test logins (development mode)

No SMS is sent while `Otp:DevMode` is `true`; these fixed codes always work.

| Who | How to log in |
|---|---|
| Patient | any valid mobile number, OTP `1234` |
| New lab partner | any valid mobile number, OTP `123456` (starts at KYC) |
| Demo lab partner (already approved, owns "Vitalis Diagnostics") | mobile `9000000001`, OTP `123456` |
| Admin (local only) | `admin@diagnx.local` / `Admin@12345` |

In Swagger: call `otp/send`, then `otp/verify`, copy the `accessToken`, click **Authorize** and paste it.

Other test helpers:
- Mock payments always succeed. Send `gatewayPaymentId: "fail"` to simulate a declined payment.
- KYC test values that pass the checksums: Aadhaar `234567890124`, business PAN `AAACS1234C`,
  GSTIN `36AAACS1234C1ZZ`, personal PAN `ABCPS1234D`, CIN `U85110TG2015PTC123456`, IFSC `HDFC0001234`.

## Tests

```bash
dotnet test
```

```bash
python tests/e2e/smoke.py
```

`dotnet test` runs the validation-rule unit tests (no database needed). `smoke.py` walks both full
journeys against a running server: patient books → pays → lab collects → results → report → review, and
new lab → 6-step KYC → admin approval → lab sets a price → appears in the patient's lab comparison.

## How the API is organised

Base path `/api/v1`. Every response uses the same envelope:

```json
{ "success": true, "data": { }, "requestId": "..." }
```

```json
{ "success": false, "error": { "code": "VALIDATION_FAILED", "message": "...", "fields": [ { "field": "ifsc", "code": "INVALID_IFSC", "message": "..." } ] }, "requestId": "..." }
```

| Prefix | For | Covers |
|---|---|---|
| `/patient` | Patient app | OTP login, home, search, compare labs, lab profile, slots, bookings, payment, tracking, reports, reviews, profile, addresses, family members, notifications |
| `/partner` | Partner app | OTP login, KYC wizard + document uploads, then (after approval) dashboard, test menu & prices, phlebotomists, orders, sample collection, result entry |
| `/master` | Both apps | Dropdowns, runtime config, PIN code and IFSC lookup |
| `/admin` | Back-office | KYC review, test catalog + report templates, labs, bookings, config |

### Partner spec mapping

The partner endpoints implement `docs/DiagnX_Partner_API_Spec.xlsx` from the partner app. Field names, enums,
validation rules and error codes are as specified; paths gained a `/partner` prefix because one API serves both apps.

| Spec id | Endpoint |
|---|---|
| AUTH-01 / 02 / 03 | `POST /partner/auth/otp/send` · `/verify` · `/resend` |
| AUTH-04 / 05 | `POST /partner/auth/token/refresh` · `/partner/auth/logout` |
| PRT-01 / 02 | `GET /partner/me` · `POST /partner/devices` |
| KYC-01 / 02 | `GET /partner/kyc/progress` · `/partner/kyc/application` |
| KYC-03 … 07 | `PUT /partner/kyc/application/profile` · `licences` · `owner` · `bank` · `operations` |
| KYC-08 / 09 | `POST /partner/kyc/application/submit` · `GET /partner/kyc/status` |
| DOC-01 / 02 / 03 | `POST /partner/kyc/documents` · `DELETE …/{id}` · `GET …/{id}/url` |
| MST-01 … 04 | `GET /master/options` · `config` · `ifsc/{ifsc}` · `pincode/{pincode}` |
| ADM-01 … 04 | `GET /admin/kyc/applications` · `…/{id}` · `POST …/{id}/decision` · `…/{id}/verify` |

### Order statuses

`payment_pending → confirmed → enroute (home only) → collected → processing → ready → completed`, or `cancelled`.
The lab moves the order forward; collecting the sample needs the 4-digit code shown in the patient's app.
Publishing the report moves it to `ready`; the patient's review moves it to `completed`.

## Deploy to Render

1. Push this folder to a GitHub repository.
2. In Render: **New → Blueprint**, choose the repository. `render.yaml` creates the API (Docker) and a PostgreSQL database.
3. When asked, enter `Admin__Email` and `Admin__Password` — this becomes the first admin login.
4. After the deploy finishes, open `https://<your-service>.onrender.com/swagger`.

Tables and demo data are created automatically on the first start.

Render free-plan limits: the API sleeps after 15 minutes without traffic (the next request takes about a
minute), and the free database is deleted after 30 days. Move both to a paid plan before real use.

## Before real users

- **Turn off dev OTP.** With `Otp__DevMode=true` anyone can log in as any phone number using the fixed code.
  Implement `ISmsSender` for your SMS provider, register it in `Program.cs`, then set `Otp__DevMode=false`.
- **Payments.** Implement `IPaymentGateway` for Razorpay and register it in place of `MockPaymentGateway`.
- **Demo data.** Set `Seed__DemoData=false` on a fresh production database so the demo labs and the demo
  partner login are not created.
- **`Security__MasterKey`** encrypts Aadhaar, PAN and bank account numbers. Never change it once data exists,
  and keep a backup of it.
- **Files** (KYC documents, report PDFs) are stored in PostgreSQL behind `IFileStorage`. Move to S3 / R2 when volume grows.
- **KYC verification checks** (PAN, GSTIN, bank penny-drop…) are recorded manually by the admin until a provider is chosen.
- Push notifications are not sent yet; device tokens are stored and in-app notifications work.
