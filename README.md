# Rentilo

Rentilo is a peer-to-peer rental marketplace: rent tools, gear and gadgets from people nearby, or earn money from the things you already own.

Built with **ASP.NET Core 10 Razor Pages**, **ASP.NET Core Identity**, **EF Core 10** and **PostgreSQL**.

## Features

- **Browse & search** by keyword, category, location and price, with sorting and pagination.
- **Listings** with photo upload, categories, a daily price and a pause switch. Only the owner can edit a listing.
- **Booking requests**: pick dates and see the total price live. The price is always recalculated on the server.
- **Owner dashboard**: stats, listings, and incoming requests to accept, decline or mark as returned.
  Accepting a request automatically declines other pending requests for the same dates.
- **No double bookings**: enforced in the application *and* by a PostgreSQL exclusion constraint, so it also holds under concurrent requests.
- **My rentals**: withdraw or cancel a booking (until the day it starts), then leave a 1–5★ review once the rental is over.
- **Messages**: every booking has its own conversation between renter and owner, with unread badges and read receipts.
- **Email notifications** for new requests, accepted/declined requests, cancellations and new messages. They are sent in the background, so a slow mail server never blocks the app.
- **Moderation**: users can report listings. Admins (configured by email) get `/Admin`, where they can work the reports queue, hide or unhide listings with a note to the owner, lock or unlock users and delete reviews.
- **Public profiles** with listings and average rating.
- **Accounts**: registration, login with lockout, profile, email and password changes, 2FA, personal data download, and full account deletion.
- **Design**: responsive, light and dark theme, accessible markup, and a strict Content-Security-Policy.

## Quick start (Docker)

```bash
docker compose up --build
```

Open <http://localhost:8080>. The database is migrated on startup and demo data is seeded. To start without demo data, set `SEED_DEMO_DATA=false`.

**Demo accounts** (password `Rentilo123!`): `anna@rentilo.dev`, `erik@rentilo.dev`, `sara@rentilo.dev`.

## Deploying to a server

See **[deploy/README.md](deploy/README.md)**. One PowerShell command (`.\deploy\deploy.ps1`) builds the app with Docker Desktop and deploys it over SSH, with HTTPS, PostgreSQL and nightly backups.

## Local development

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and PostgreSQL 14 or newer.

1. **Set the connection string with user secrets.** Never put it in `appsettings.json`.

   ```bash
   cd src/Rentilo
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=rentilo;Username=postgres;Password=<your-password>"
   ```

2. **Run the app.** In Development, migrations are applied and demo data is seeded automatically.

   ```bash
   dotnet run --project src/Rentilo
   ```

3. **Run the tests.** They use SQLite in-memory, so no database is needed.

   ```bash
   dotnet test
   ```

### Database migrations

The `dotnet-ef` tool is pinned in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Rentilo -o Data/Migrations
dotnet ef database update --project src/Rentilo
```

The `InitialCreate` migration enables the `btree_gist` extension, which is trusted in PostgreSQL 13 and later, so the database owner can enable it. It also adds the `EX_Bookings_NoOverlap` exclusion constraint.

## Configuration

| Setting | Default | Description |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | — (required) | PostgreSQL connection string. Set it with user secrets or `ConnectionStrings__DefaultConnection`. |
| `Database:MigrateOnStartup` | `false` (`true` in Development) | Apply EF migrations when the app starts. |
| `Database:SeedDemoData` | `false` (`true` in Development) | Create demo users and listings if the database has no items. |
| `Admin:Emails` | empty (`anna@rentilo.dev` in Development) | Comma-separated emails that get the Admin role, applied at startup and on registration. |
| `App:BaseUrl` | empty | Public URL used in email links when no web request is available. |
| `DataProtection:KeysPath` | empty (`/app/keys` in Docker) | Folder where login-cookie keys are kept, so restarts don't sign users out. |
| `Identity:RequireConfirmedAccount` | `false` | Require email confirmation before login. Turn this on once SMTP is configured. |
| `Email:Smtp:Host` / `Port` / `EnableSsl` / `UserName` / `Password` / `From` | empty | SMTP settings for confirmation and password-reset mail. If `Host` is empty, emails are written to the log instead. |

Health check: `GET /healthz` (includes a database check).

## Project structure

```
src/Rentilo/
  Data/              DbContext, migrations, demo seed data
  Models/            Item, Booking, Review, ApplicationUser, enums
  Services/          BookingService (booking rules), MessageService, ModerationService,
                     EmailNotifier + background email queue, image storage, account deletion
  Infrastructure/    Security headers middleware
  ViewModels/        Item cards, form models, pagination
  Pages/             Razor Pages: Items, Bookings (incl. messages), Dashboard, Admin, Users, static pages
  Areas/Identity/    Customised Identity pages (login, register, account settings)
  wwwroot/           CSS design system, JS, vendored Bootstrap 5.3 / jQuery 3.7
tests/Rentilo.Tests/ Unit tests (booking rules, helpers) and integration tests (WebApplicationFactory)
```

## Booking rules

- Dates are whole days and inclusive: Mon–Wed is 3 days. Price = days × price per day.
- A booking can't start in the past, can last at most 60 days and can start at most a year ahead.
- You can't rent your own item, or an item that is paused.
- Pending requests may overlap. Only **accepted** and **completed** bookings block dates.
- The renter can withdraw a pending request at any time, and cancel an accepted booking before its start date.
- The owner can mark a booking as returned once it has started.
- A review is allowed once per booking, after it is completed or its end date has passed.
- A listing with booking history is paused rather than deleted, so renters keep their records.

## Security notes

- Passwords: at least 8 characters with a digit and a lowercase letter. 5 failed logins lock the account for 15 minutes.
- Every form uses antiforgery tokens. Forms bind to input models rather than entities, which prevents over-posting.
- Ownership is checked on the server for every edit, delete, accept, decline, complete, cancel and review.
- Uploaded images are limited to 5 MB, checked by their file signature (JPEG, PNG, WebP, GIF) and stored under random names.
- The CSP allows only same-origin scripts and styles, with no inline code. The app also sends `X-Frame-Options: DENY`, `nosniff` and a strict referrer policy.
- Contact details (email) are shared only between the two people in an accepted booking.

## Deployment notes

- The Docker image listens on port 8080 and runs as a non-root user. Mount a volume at `/app/wwwroot/uploads` so photos survive redeploys.
  For multiple instances, replace `LocalImageStorage` with blob storage (implement `IImageStorage`).
- Put the app behind a TLS-terminating proxy. Forwarded headers are honoured, and HSTS is enabled outside Development.
- In production, set `Database:MigrateOnStartup=true`, or run `dotnet ef database update` as a release step.
