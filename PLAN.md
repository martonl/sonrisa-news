## Plan: Sonrisa-News Full Application Build

A JWT-secured .NET 10 modular monolith (`NewsApp`) with a Vue 3 admin SPA, an AI-powered news evaluator agent using `Microsoft.Agents.AI.OpenAI`, a .NET Channel queue, stub email/Slack senders, and xUnit integration + API tests. No Docker for now.

---

### Phase 1 — Solution Scaffold & Infrastructure
*(all steps parallel-safe within the phase)*

1. Create `SonrisaNews.sln` at repo root
2. Create `src/NewsApp` — `dotnet new webapi --framework net10.0`
3. Create `tests/NewsApp.Tests` — `dotnet new xunit --framework net10.0`
4. Add NuGet packages to `NewsApp`: `Identity.EntityFrameworkCore`, `EF Sqlite`, `EF Design`, `JwtBearer`, `Microsoft.Agents.AI.OpenAI --prerelease`, `System.ServiceModel.Syndication`
5. Add NuGet to tests: `Microsoft.AspNetCore.Mvc.Testing`, `EF InMemory`
6. Create `AppDbContext : IdentityDbContext<ApplicationUser>` in `Infrastructure/Data/`
7. Seed `appsettings.json` with `ConnectionStrings`, `Jwt:*`, `OpenAI:*`, `NewsEvaluator:*` sections
8. Wire up Identity, JWT, EF Core, Swagger in `Program.cs` using per-module `AddXxxModule()` extension methods

---

### Phase 2 — Identity & Auth Module (`Modules/Identity/`)
*(depends on Phase 1)*

1. `ApplicationUser : IdentityUser` (minimal)
2. `JwtTokenService` — generates JWT from user claims with configurable expiry
3. `AuthController`:
   - `POST /api/auth/register`
   - `POST /api/auth/login` → returns token
   - `GET /api/auth/me` [Authorize]
4. Startup seed: create `Admin` role + first admin user from config
5. Write `AuthApiTests` for register, login, and me

---

### Test Phase 2 — Identity & Auth Validation
Run these tests at the end of Phase 2 before starting Phase 3.

1. `dotnet build`
2. `dotnet test` focusing on `AuthApiTests` for register, login, and me
3. `dotnet run` in `src/NewsApp` and verify `/api/auth/register`, `/api/auth/login`, and `/api/auth/me` through Swagger or a REST client

---

### Phase 3 — Subscriptions Module (`Modules/Subscriptions/`)
*(depends on Phase 2)*

1. `Subscription` entity: `Id (Guid)`, `UserId`, `Type (Email|Slack)`, `Target`, `IsActive`, `CreatedAt`
2. EF migration `InitialCreate` (Identity + Subscription + AgentRunState tables)
3. Subscriptions endpoints [Authorize]: full CRUD with ownership enforcement
4. Admin endpoints [Authorize(Roles="Admin")]:
   - `GET /api/admin/users` — paginated
   - `GET|POST|PUT|DELETE /api/admin/users/{id}/subscriptions`
5. Write `SubscriptionApiTests` and `AdminApiTests` for testing new endpoints

---

### Test Phase 3 — Subscriptions Validation
Run these tests at the end of Phase 3 before starting Phase 4.

1. `dotnet build`
2. `dotnet test` focusing on `SubscriptionApiTests` and `AdminApiTests`
3. `dotnet ef migrations add InitialCreate` to confirm the combined Identity and Subscription model is valid

---

### Phase 4 — Notification Infrastructure (`Modules/Notifications/`)
*(depends on Phase 3; parallel with Phase 5)*

1. `NewsNotification` record: `Title`, `Summary`, `Url`, `PublishedAt`
2. `INewsQueue` — `EnqueueAsync` / `ReadAllAsync` (IAsyncEnumerable)
3. `NewsChannel` — `System.Threading.Channels.Channel<NewsNotification>` singleton
4. `IEmailSender` / `ISlackSender` interfaces + `StubEmailSender` / `StubSlackSender` (log via ILogger)
5. `NotificationWorker : BackgroundService` — consumes queue, resolves active subscriptions, dispatches by type
6. Write Integration tests for verify the notification worker can consume queued `NewsNotification` items

---

### Test Phase 4 — Notification Validation
Run these tests at the end of Phase 4 before starting Phase 5.

1. `dotnet build`
2. `dotnet test` focusing on notification integration coverage for queue consumption and sender dispatch
3. Verify the notification worker can consume queued `NewsNotification` items in a local run

---

### Phase 5 — News Evaluator AI Agent (`Modules/NewsEvaluator/`)
*(depends on Phase 4 for INewsQueue; parallel with Phase 4 setup)*

1. `RssFeedService` — fetches configured RSS URLs using `SyndicationFeed.Load()`, returns `IEnumerable<RssItem>`. Use this feed: https://feeds.bbci.co.uk/news/rss.xml
2. `AgentRunState` EF entity — single-row table tracking `LastRunAt`
3. `NewsEvaluatorAgent` — wraps `OpenAIClient → GetResponsesClient() → AsAIAgent(model, systemPrompt)`, download RSS feed, parse it, filter it with `LastRunAt` then sends remaining batch for AI processing, parses JSON response into `IEnumerable<NewsNotification>`. Use this prompt for getting correct JSON response: Evaluate this news feed. Return maximum 5 most important hard-news stories. Prioritize breaking international news, wars, major geopolitical developments, natural disasters, significant economic/business news, public health, science and technology, and government policy with broad international impact. Exclude sports, entertainment, celebrity, royal/family news, opinion pieces, analysis, explainers, feature stories, human-interest stories, local crime, court hearings, individual criminal cases, and lifestyle content. Return JSON with Title, Summary, Url, and PublishedAt.
4. `NewsSchedulerService : BackgroundService` — timed loop using `NewsEvaluator:IntervalMinutes`, filters items by `LastRunAt`, calls agent, enqueues results, persists updated state
5. `POST /api/admin/agent/run` [Authorize(Roles="Admin")] — manual trigger endpoint
6. Write Integration tests `NewsSchedulerServiceTests` — verify `LastRunAt` filtering, queue publication, DB state update using mock `IRssFeedService` + stub `INewsEvaluatorAgent`

---

### Test Phase 5 — News Evaluator Validation
Run these tests at the end of Phase 5 before starting Phase 6.

1. `dotnet build`
2. `dotnet test` focusing on `NewsSchedulerServiceTests` for `LastRunAt` filtering, queue publication, and DB state updates
3. Verify the scheduler updates `AgentRunState` and publishes notifications into the queue

---

### Phase 6 — Vue.js Admin Site (`src/NewsAdmin/`)
*(depends on Phases 3 & 5 for the API; can start in parallel)*

1. `npm create vite@latest NewsAdmin -- --template vue` in `src/`
2. Install `vue-router@4`, `pinia`, `axios`
3. Vite proxy in `vite.config.js`: `/api` → `http://localhost:5000`
4. `authStore` (Pinia) with JWT persistence in localStorage
5. Axios interceptor for `Authorization: Bearer` header
6. Views: `LoginView`, `UsersView`, `SubscriptionsView`, `DashboardView` (with agent trigger button)
7. `router.beforeEach` guard redirecting unauthenticated to `/login`

---

### Solution Structure

```
sonrisa-news/
├── src/
│   ├── NewsApp/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Infrastructure/
│   │   │   └── Data/
│   │   │       ├── AppDbContext.cs
│   │   │       └── Migrations/
│   │   └── Modules/
│   │       ├── Identity/
│   │       │   ├── ApplicationUser.cs
│   │       │   ├── AuthController.cs
│   │       │   ├── JwtTokenService.cs
│   │       │   └── IdentityModule.cs
│   │       ├── Subscriptions/
│   │       │   ├── Subscription.cs
│   │       │   ├── SubscriptionsController.cs
│   │       │   └── SubscriptionsModule.cs
│   │       ├── Notifications/
│   │       │   ├── INewsQueue.cs
│   │       │   ├── NewsChannel.cs
│   │       │   ├── NewsNotification.cs
│   │       │   ├── NotificationWorker.cs
│   │       │   ├── Senders/
│   │       │   │   ├── IEmailSender.cs
│   │       │   │   ├── StubEmailSender.cs
│   │       │   │   ├── ISlackSender.cs
│   │       │   │   └── StubSlackSender.cs
│   │       │   └── NotificationsModule.cs
│   │       └── NewsEvaluator/
│   │           ├── RssItem.cs
│   │           ├── RssFeedService.cs
│   │           ├── AgentRunState.cs
│   │           ├── NewsEvaluatorAgent.cs
│   │           ├── NewsSchedulerService.cs
│   │           └── NewsEvaluatorModule.cs
│   └── NewsAdmin/               (Vue 3 + Vite)
│       ├── src/
│       │   ├── main.js
│       │   ├── router/index.js
│       │   ├── stores/authStore.js
│       │   ├── api/http.js
│       │   └── views/
│       │       ├── LoginView.vue
│       │       ├── DashboardView.vue
│       │       ├── UsersView.vue
│       │       └── SubscriptionsView.vue
│       ├── vite.config.js
│       └── package.json
└── tests/
    └── NewsApp.Tests/
        ├── CustomWebApplicationFactory.cs
        ├── TestJwtHelper.cs
        ├── ApiTests/
        │   ├── AuthApiTests.cs
        │   ├── SubscriptionApiTests.cs
        │   └── AdminApiTests.cs
        └── AgentTests/
            └── NewsSchedulerServiceTests.cs
```

---

### Key Configuration (`appsettings.json` shape)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=news.db"
  },
  "Jwt": {
    "Secret": "<min-32-char-secret>",
    "Issuer": "sonrisa-news",
    "Audience": "sonrisa-news-clients",
    "ExpiryMinutes": 60
  },
  "OpenAI": {
    "ApiKey": "<your-openai-api-key>",
    "Model": "gpt-4o-mini"
  },
  "NewsEvaluator": {
    "IntervalMinutes": 15,
    "RssSources": [
      "https://feeds.bbci.co.uk/news/rss.xml",
      "https://rss.nytimes.com/services/xml/rss/nyt/HomePage.xml"
    ]
  },
  "AdminSeed": {
    "Email": "admin@example.com",
    "Password": "Admin123!"
  }
}
```

---

### Verification Checklist

- [ ] `dotnet build` — zero errors across solution
- [ ] `dotnet ef migrations add InitialCreate` — migration generated successfully
- [ ] `dotnet run` in `src/NewsApp` — Swagger UI accessible, JWT auth flow works end-to-end
- [ ] `dotnet test` — all API tests and scheduler integration tests pass
- [ ] `npm run dev` in `src/NewsAdmin` — login with admin credentials, manage subscriptions, trigger agent via UI

---

### Notes

- `Microsoft.Agents.AI.OpenAI` is prerelease — if unstable, fall back to the official `OpenAI` SDK with `Microsoft.Extensions.AI.IChatClient`
- `AdminSeed:Password` must only appear in `appsettings.Development.json`, never committed in production config
- `AgentRunState` uses a single-row upsert pattern (`Id = 1` always); no need for a full history table at MVP
- Phases 4 and 5 can be built in parallel once Phase 3 is complete
- Integration tests mock `INewsEvaluatorAgent` (not the OpenAI HTTP layer) to avoid network calls in CI
