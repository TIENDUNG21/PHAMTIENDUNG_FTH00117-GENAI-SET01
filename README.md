# Smart Shop - Generative AI for .NET Developers (Set 01)

An ASP.NET Core 10 (.NET 8+ compatible) Web API that uses **Google Gemini** to write product
descriptions, analyze customer review sentiment, and power a catalog-aware shopping assistant
for the fictional **Smart Shop** electronics store. Includes a small static front end that shows
the review report.

## 1. Tech stack

- **.NET 10 SDK** (the project targets `net10.0`; the task only requires .NET 8 or later, and
  this is the only SDK installed in the dev environment used to build it — it is fully backward
  compatible with the .NET 8 API surface used here. To target `net8.0` instead, change
  `<TargetFramework>` in `SmartShop.Api.csproj` and install the .NET 8 SDK.)
- ASP.NET Core Web API (Controllers)
- Entity Framework Core 10 + SQL Server (Code First, migrations included)
- Gemini REST API called directly via `HttpClient` (model `gemini-2.5-flash`), no third-party
  Gemini SDK dependency
- Vanilla HTML/CSS/JS front end served from `wwwroot/`
- Swagger / OpenAPI (`/swagger`) for interactively testing every endpoint

## 2. Project layout

```
SmartShop.slnx
SmartShop.Api/                  ASP.NET Core Web API project
  Controllers/                  ProductsController, ReviewsController, AssistantController, ReportsController
  Models/                       Product, Review (EF entities) + Dtos/
  Data/                         SmartShopDbContext (seed data for the 4 products) + Migrations/
  Services/Gemini/              IGeminiClient + GeminiClient (raw REST calls), wire DTOs, exceptions
  Services/Prompts/             PromptTemplates.cs - single source of truth for every prompt (see section 5)
  Services/                     IChatHistoryStore + InMemoryChatHistoryStore (per-session chat history)
  Middleware/                   ExceptionHandlingMiddleware (centralized error handling, see section 7)
  wwwroot/                      Static front end (review report table)
Database/schema.sql             Idempotent SQL script (CREATE DATABASE + tables + seed data)
SmartShop.postman_collection.json  Postman collection covering every endpoint
```

## 3. Database setup

The project is **Code First**. `Database/schema.sql` is an idempotent SQL Server script generated
with `dotnet ef migrations script --idempotent` from the EF Core model — run it directly in SSMS /
`sqlcmd` if you prefer not to let the app apply migrations itself:

```
sqlcmd -S <server> -E -i Database/schema.sql
```

Alternatively, just run the API (`dotnet run` from `SmartShop.Api/`): `Program.cs` calls
`db.Database.Migrate()` on startup, which creates the `SMARTSHOP` database, the `Product` and
`Review` tables, and seeds the 4 initial products automatically.

Connection string (`ConnectionStrings:SmartShopDb` in `appsettings.json`, overridden for
local development in `appsettings.Development.json` to use LocalDB):

```
Server=localhost;Database=SMARTSHOP;Trusted_Connection=True;TrustServerCertificate=True;
```

Adjust the server name for your own SQL Server instance (or switch the provider to MySQL by
swapping the `Microsoft.EntityFrameworkCore.SqlServer` package/`UseSqlServer` call for
`Pomelo.EntityFrameworkCore.MySql`/`UseMySql`, then regenerating the migration).

## 4. Configuring the Gemini API key (never hard-coded)

The key is read from ASP.NET Core configuration under `Gemini:ApiKey` and is **not present** in
any committed file (`appsettings.json` ships with an empty string). Two supported ways to supply
it, both outside of source control:

**Development — .NET User Secrets** (stored outside the repo, under your user profile):

```
cd SmartShop.Api
dotnet user-secrets init          # already done; UserSecretsId is in the .csproj
dotnet user-secrets set "Gemini:ApiKey" "<your-gemini-api-key>"
```

**Any environment — environment variable** (either form works; `Program.cs` checks both):

```
# PowerShell
$env:GEMINI__ApiKey = "<your-gemini-api-key>"
# or
$env:GEMINI_API_KEY = "<your-gemini-api-key>"
```

At request time, `GeminiClient` sends the key in the `x-goog-api-key` HTTP header (never in the
query string), so it never ends up in server access logs, browser history, or proxy logs. If the
key is missing, the API fails fast with a clear `500 Service misconfigured` response instead of a
raw exception (see section 7) — it never silently calls Gemini with an empty key.

## 5. Prompts used (system instruction + user prompt)

All prompt text lives in one place, `SmartShop.Api/Services/Prompts/PromptTemplates.cs`, so the
wording below is guaranteed to match the running code.

### Requirement 1 - `POST /api/products/{id}/generatedescription`

**System instruction:**
> You are a professional copywriter for an online electronics store named Smart Shop. Given a
> product's name, category and key features, write an engaging, accurate marketing description in
> English. The description must be at most 80 words, written as flowing prose (not bullet points),
> must highlight the most compelling features, and must not invent any specification that was not
> provided. Do not use markdown formatting.

**User prompt** (templated from the product row):
```
Product name: {ProductName}
Category: {Category}
Key features: {Features}

Write the product description now.
```

Generation config: `temperature = 0.7`, `maxOutputTokens = 300`. The returned text is saved into
`Product.Description` and returned as-is.

### Requirement 2 - `POST /api/reviews/analyze`

**System instruction:**
> You are a review-analysis engine for an e-commerce platform called Smart Shop. Given a
> customer's review text and star rating, determine the sentiment and return ONLY a JSON object
> that matches the required schema. 'sentiment' must be exactly one of: Positive, Negative,
> Neutral. 'score' is a number between 0.0 and 1.0 representing your confidence in the sentiment
> classification. 'summary' is a concise, single-sentence summary of the review written in
> English, at most 40 words. Do not include any text outside the JSON object.

**User prompt** (templated from the submitted review):
```
Customer rating: {Rating}/5 stars
Review content: "{Content}"

Analyze this review.
```

Generation config: `temperature = 0.2`, `responseMimeType = "application/json"`, plus a
`responseSchema` that constrains the output to:

```json
{
  "type": "OBJECT",
  "properties": {
    "sentiment": { "type": "STRING", "enum": ["Positive", "Negative", "Neutral"] },
    "score": { "type": "NUMBER" },
    "summary": { "type": "STRING" }
  },
  "required": ["sentiment", "score", "summary"]
}
```

The response text is deserialized into `GeminiReviewAnalysis` and saved into the `Review` row's
`Sentiment`, `Score` and `Summary` columns alongside the original review.

### Requirement 3 - `POST /api/assistant/chat`

**System instruction** (rebuilt on every call from the live `Product` table, so it always
reflects current stock/prices):
> You are the shopping assistant for Smart Shop, an online electronics store. You must answer
> ONLY questions about the products listed in the CATALOG section below (availability, specs,
> features, price, and comparisons between them). If the user asks anything unrelated to these
> products - general knowledge, other stores, personal advice, or anything not covered by the
> catalog - politely refuse and explain that you can only help with Smart Shop's product catalog.
> Always answer in English, be concise, and never invent specs or prices that are not listed in
> the catalog.
>
> CATALOG:
> - {ProductName} | Category: {Category} | Price: {Price} VND | Features: {Features}
> - ... one line per product ...

**User turn:** the raw `message` field from the request body, appended to the stored
conversation history for that `sessionId` (see `IChatHistoryStore`), so a follow-up question like
"How much does it cost?" resolves against the previous turn ("Which phone has the biggest
battery?"). History is kept in memory, capped at the last 20 turns per session, and expires after
30 minutes of inactivity.

Generation config: `temperature = 0.4`, `maxOutputTokens = 400`.

## 6. All API endpoints

| # | Method | Route | Purpose |
|---|--------|-------|---------|
| - | GET | `/api/products` | List products (helper, not part of the grading requirements) |
| 1 | POST | `/api/products/{id}/generatedescription` | Generate + save a product description |
| 2 | POST | `/api/reviews/analyze` | Analyze a review's sentiment (JSON mode) and save it |
| 3 | POST | `/api/assistant/chat` | Catalog-only shopping assistant with per-session history |
| 4 | GET | `/api/reports/reviews` | Aggregated review report (used by the front end) |

Full request/response examples are in `SmartShop.postman_collection.json`; everything is also
explorable live via Swagger UI at `/swagger` once the app is running.

## 7. Error handling (bonus requirement)

`Middleware/ExceptionHandlingMiddleware.cs` wraps the whole request pipeline and converts
failures into consistent `application/problem+json` responses instead of letting them crash the
process or leak stack traces:

- **Gemini quota / rate limit (`429 Too Many Requests`)** → re-thrown as `GeminiApiException`
  with `IsQuotaExceeded = true`, mapped to `429` with a "please retry shortly" message.
- **Any other non-success Gemini response** (bad request, auth failure, 5xx, ...) → mapped to
  `502 Bad Gateway`, with the real Gemini error message surfaced and the full response logged.
- **Invalid / unparsable JSON from Gemini** (either the HTTP body itself, or — for
  `analyzereview` — a response that doesn't deserialize into `sentiment`/`score`/`summary`, or
  uses a `sentiment` value outside `Positive/Negative/Neutral`) → `GeminiInvalidJsonException`,
  mapped to `502 Bad Gateway`; the raw Gemini text is logged server-side for diagnostics but never
  echoed back verbatim to the client.
- **Missing/blank API key** → fails fast with `500 Service misconfigured` and an actionable
  message, instead of sending an empty key to Gemini.
- **Unknown product id** → plain `404 Not Found` from the controller (not routed through Gemini
  at all).
- **Any other unhandled exception** → generic `500` ProblemDetails; full exception logged
  server-side, never exposed to the caller.

Request DTOs also use data annotations (`[Required]`, `[Range(1,5)]` for rating, `[MaxLength]`)
so malformed input is rejected by model binding with a standard `400` before it ever reaches
Gemini.

## 8. Running the project

```
cd SmartShop.Api
dotnet user-secrets set "Gemini:ApiKey" "<your-key>"   # once
dotnet run
```

Then open:
- `https://localhost:<port>/` - the review report front end (requirement 5)
- `https://localhost:<port>/swagger` - interactive API docs for requirements 1-4
