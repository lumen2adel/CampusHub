# CampusHub

A social and communication platform for university students: real-time chat, interest groups with posts, polls and events, and live notifications, served as a REST + SignalR API.

CampusHub was my graduation project at **Al-Mamoun University College** (Baghdad, 2025). This repository is the backend: an **ASP.NET Core Web API** on **PostgreSQL**.

## Features

**Accounts and security**
- Registration with email verification codes, password recovery and reset
- JWT access tokens plus refresh tokens, with active-session listing, single-session revoke, and "log out everywhere"
- BCrypt password hashing, Google reCAPTCHA on sign-in, and IP-based rate limiting on login (5 requests per minute)
- Role model: `SuperAdmin`, `Admin`, `User`

**Social graph**
- Friend requests (send, accept, reject) and friend lists
- User profiles and activity statistics

**Real-time chat (SignalR, `/chathub`)**
- One-to-one and group chats
- Attachments, reactions, edits, replies, forwarding and pinned messages
- Typing indicators, read/seen receipts, online status, unread counts, message search

**Groups and posts**
- Public and private groups, join requests, admin management
- Posts with likes, comments, shares, `@mentions`, reports and moderator flagging
- Group announcements, events with participants, polls with live results
- Personalized home feed, plus trending and hot posts
- Full-text search over posts using PostgreSQL `tsvector` with a trigger-maintained search column

**Notifications (SignalR, `/notificationHub`)**
- PostgreSQL `LISTEN/NOTIFY` feeds a background service that pushes events to connected clients over SignalR

## Tech stack

| Area | Technology |
|---|---|
| Framework | ASP.NET Core 8 Web API |
| Database | PostgreSQL, Entity Framework Core 8 (Npgsql) |
| Real-time | SignalR |
| Auth | JWT bearer, refresh tokens, BCrypt |
| Background work | `BackgroundService` hosted services |
| Other | AutoMapper, SMTP email (`System.Net.Mail`), Google reCAPTCHA, Swagger / OpenAPI |

## Running locally

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download) and PostgreSQL 14 or later.

1. **Clone the repo and restore the tools**

   ```bash
   git clone https://github.com/lumen2adel/CampusHub.git
   cd CampusHub/CampusHub
   dotnet tool restore
   ```

2. **Configure secrets.** None are committed; see [`appsettings.Example.json`](CampusHub/appsettings.Example.json) for every key. For local development, use [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

   ```bash
   dotnet user-secrets set "ConnectionStrings:DataContext" "Host=localhost;Port=5432;Database=campushub;Username=postgres;Password=<your-password>"
   dotnet user-secrets set "jwt:Secret" "<random string, at least 32 characters>"
   dotnet user-secrets set "EmailSettings:Email" "<sender address>"
   dotnet user-secrets set "EmailSettings:Password" "<SMTP app password>"
   dotnet user-secrets set "Captcha:SecretKey" "<reCAPTCHA secret key>"
   ```

   In Docker or production, use environment variables instead. Replace each `:` with `__`, for example `jwt__Secret`.

3. **Create the database and run the API**

   ```bash
   dotnet ef database update
   dotnet run --launch-profile http
   ```

4. Open **http://localhost:5007/swagger**.

## API overview

All endpoints are documented and testable through **Swagger UI** at `/swagger`. To call protected endpoints, use the **Authorize** button and paste a JWT from `POST /api/Account/login`.

| Controller | Base route | Purpose |
|---|---|---|
| Account | `/api/Account/*` | Register, verify, login, refresh, sessions, password recovery, profile, friends |
| Chat | `/api/Chat/*` | Direct and group messaging, attachments, reactions, pins, chat notifications |
| Group | `/api/Group/*` | Groups, membership, posts, feed, search, events, polls, announcements, moderation |
| UserStatistics | `/api/UserStatistics/*` | Activity tracking and user statistics |

| SignalR hub | Path | Pushes |
|---|---|---|
| ChatHub | `/chathub` | Messages, typing, reactions, edits, read receipts, friend requests |
| NotificationHub | `/notificationHub` | Database-driven notifications |

## Known gaps

This is a student project, and these items are tracked for improvement:

- The `trending_posts` and `hot_posts` materialized views and the `new_notification` trigger were created by hand on the original server and are not in the EF migrations yet. On a fresh database, the trending, hot-posts and notification background services log errors.
- There are no automated tests yet.

## Author

**Laith Adel**, backend developer
