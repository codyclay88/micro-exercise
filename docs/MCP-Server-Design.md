# Design Doc: MCP Server for Micro-Burst

**Status:** Proposal / scope assessment — *not committed to implementation.*
**Author:** (design exploration)
**Date:** 2026-06-10
**Related:** `docs/Micro-Burst Exercise Tracker Spec.md`, `CLAUDE.md`

> **Purpose of this doc.** Capture what it would take to expose Micro-Burst to AI
> assistants (Claude.ai, Claude Desktop/mobile, Claude Code) via a remote **Model
> Context Protocol** server, so a user could log bursts and query their history/reports
> by talking to their assistant. This is written to **size the change**, not to lock in
> a build. §8 (Scope Assessment) is the part to read if you only read one section.

---

## 1. Goal & non-goals

**Goal.** Let an authenticated user connect Micro-Burst to their AI assistant as a
"custom connector" and:

- query their pool, history, and summary reports, and
- log a micro-burst,

…in natural language, with the assistant calling tools on a server we host.

**Non-goals (for the initial design).**

- Editing or deleting history, or mutating the pool, *through the assistant* — see the
  deliberately narrow tool surface in §5. These are additive later.
- A public/multi-tenant connector listed in any directory. This is for our own users
  authenticating against their existing Micro-Burst accounts.
- Replacing or changing the existing Blazor WASM SPA or REST API in any way.

---

## 2. Why this is mostly an OAuth project

A remote MCP server that serves *multiple users* is, by spec, an **OAuth 2.1 Resource
Server**. There is no API-key shortcut — Claude's connector flow and the MCP
authorization spec both require an OAuth authorization-code + PKCE flow. The MCP endpoint
itself is small; **the bulk of the work is standing up an OAuth Authorization Server**,
which the app does not have today (it uses cookie-based ASP.NET Core Identity, which is
not an OAuth server).

The connection handshake Claude performs:

1. Claude POSTs to the MCP endpoint with no token → server returns **401** with
   `WWW-Authenticate: Bearer resource_metadata="https://<host>/.well-known/oauth-protected-resource"`.
2. Claude fetches the **Protected Resource Metadata** (RFC 9728) → learns the
   authorization server's issuer URL.
3. Claude fetches **Authorization Server Metadata** (RFC 8414 / OIDC discovery),
   registers a client (**Dynamic Client Registration**, RFC 7591), and starts an
   **authorization-code flow with PKCE (S256)**.
4. The user is sent to **our existing `/login` page**, signs in, and consents.
5. Claude receives an access token (JWT) and sends it as `Authorization: Bearer …` on
   every subsequent MCP request. The server validates the JWT and derives the user.

```
Claude  ──(1) MCP call, no token──────────────►  Resource Server (MCP endpoint)
        ◄──(1) 401 + WWW-Authenticate──────────
        ──(2) GET /.well-known/oauth-protected-resource►
        ◄──(2) { authorization_servers: [issuer] } ─
        ──(3) GET AS metadata, DCR, /authorize (PKCE)►  Authorization Server (OpenIddict)
        ◄──(4) user logs in at /login, consents ───
        ──(3) POST /token (code + verifier) ──────►
        ◄──(3) access_token (JWT, sub = userId) ──
        ──(5) MCP call + Bearer JWT ──────────────►  Resource Server → existing services
```

---

## 3. Why the service layer makes this cheap

Every service method already takes an explicit `int userId`
(`ILogService.LogAsync(int userId, …)`, `IReportService.GetSummaryAsync(int userId, …)`,
etc.), and the REST API derives the user from `ICurrentUser` rather than an ambient
accessor. The MCP tools become a **third caller** of those same services, alongside the
WASM client's REST calls — the JWT's subject claim simply replaces the auth cookie as the
source of `userId`.

**Consequence: no changes to Core or Infrastructure.** The entire feature is additive in
the **Web** project plus one EF migration.

---

## 4. Components

### 4.1 Authorization Server — OpenIddict

[OpenIddict](https://documentation.openiddict.com/) (free/OSS) integrates directly with
EF Core + ASP.NET Core Identity (int keys are fine). It provides the
`/authorize` and `/token` endpoints, server metadata, PKCE, refresh-token rotation, and
**Dynamic Client Registration** — everything Claude's flow needs — and reuses our existing
static-SSR `/login` page as the login/consent UI.

- Registers its stores on `AppDbContext`; adds its tables (applications / authorizations /
  tokens / scopes) via a migration.
- Issues JWTs whose `sub` claim is the `ApplicationUser.Id`.
- Coexists with the existing Identity **cookie** scheme — cookie auth continues to serve
  the SPA and `/api`; bearer auth serves MCP. The two are independent.

*(Alternatives considered: Duende IdentityServer — same capability, commercial license
above a revenue threshold; a managed IdP like Auth0/Entra — less code but users would
authenticate against the IdP instead of our own Identity store, and it adds an external
dependency/cost. OpenIddict chosen to stay self-hosted and license-free.)*

### 4.2 Resource Server + MCP endpoint — `ModelContextProtocol.AspNetCore`

The official C# MCP SDK (v1.0, released 2026-03; maintained with Microsoft). Verified
wiring, adapted from the SDK's `ProtectedMcpServer` sample:

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = authServerUrl;            // our OpenIddict issuer
    options.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = mcpServerUrl,             // RFC 8707 resource indicator
        ValidIssuer = authServerUrl,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
    };
})
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        AuthorizationServers = { authServerUrl },
        ScopesSupported = ["mcp:tools"],
    };
});

builder.Services.AddAuthorization();
builder.Services.AddMcpServer()
    .WithTools<MicroBurstTools>()
    .WithHttpTransport(o => o.Stateless = true);  // matches the app's stateless posture

// …after Build():
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp().RequireAuthorization();
```

The SDK serves `/.well-known/oauth-protected-resource` and emits the 401 +
`WWW-Authenticate` challenge automatically. `Stateless = true` aligns with the app's
"no SignalR/Blazor circuit" constraint and horizontal scaling.

### 4.3 User mapping

OpenIddict puts `sub = ApplicationUser.Id` in the JWT. The MCP tools resolve the current
user from the validated principal. Two clean options:

- extend `ClaimsPrincipalExtensions.GetUserId()` to also read the `sub` claim, or
- register an MCP-scoped `ICurrentUser` for the bearer pipeline.

Either way the services are untouched.

---

## 5. Tool surface (initial — read + log only)

All tools carry `[Authorize]`, derive `userId` from the token, live in the **Web**
project next to `Endpoints/ApiEndpoints.cs`, and reuse `ApiJson.Options` so `TrackingType`
serializes as a string (matching the existing REST contract).

| Tool | Underlying service call |
|---|---|
| `list_pool` | `IPoolService.GetActivePoolAsync` |
| `list_exercise_types` | `IPoolService.GetExerciseTypesAsync` |
| `get_history(from, to)` | `ILogService.GetHistoryAsync` |
| `get_summary(from, to)` | `IReportService.GetSummaryAsync` |
| `log_burst(exercisePoolId, quantity)` | `ILogService.LogAsync` |

Deliberately **excluded** for now: `update_burst`, `delete_burst`, `add_pool_item`,
`add_custom_exercise`, `update_pool_item`, `remove_pool_item`. This keeps the assistant's
blast radius to "tell me / log it." Adding the write tools later is purely additive
(new `[McpServerTool]` methods, optionally gated behind a separate `mcp:write` OAuth scope
the user must consent to).

---

## 6. Data & deployment impact

- **Migration:** one new migration adds OpenIddict's tables to the Postgres schema; runs
  through the existing startup `MigrateAsync`. Tests (SQLite in-memory) continue to work —
  OpenIddict supports it. No changes to the existing four-table schema.
- **TLS / hosting:** the OAuth flow requires HTTPS. The existing Droplet + Caddy stack
  already terminates TLS at `exercise.codyclay.com`, so no new infra is needed.
- **Redirect URIs to register:** hosted Claude surfaces use
  `https://claude.ai/api/mcp/auth_callback`; Claude Code uses port-agnostic loopback
  (`http://localhost/callback`, `http://127.0.0.1/callback`) per RFC 8252.
- **Network allowlist (optional):** Claude's requests originate from `160.79.104.0/21`.

---

## 7. Security considerations

- **Two coexisting auth schemes.** Cookie (SPA, `/api`) and JWT bearer (MCP) are separate
  pipelines; the `/api` group keeps its existing `RequireAuthorization()` +
  `DisableAntiforgery()` posture unchanged.
- **Audience binding (RFC 8707).** JWTs are validated against `ValidAudience = mcpServerUrl`
  so a token minted for another resource can't be replayed against the MCP endpoint.
- **Least privilege via tool surface.** Read + log only (see §5) bounds what a
  compromised/over-eager assistant session can do. Destructive operations stay off until
  explicitly added behind their own scope.
- **PKCE + refresh-token rotation** are required by the spec for public clients; OpenIddict
  provides both.
- **Ownership is still enforced in the services** (filtering through `ExercisePool.UserId`),
  so the MCP path inherits the same tenant isolation as the REST path — it cannot bypass it.

---

## 8. Scope assessment

**Overall size: moderate, and well-contained.** The risk/effort is concentrated in OAuth
server setup, not in the app itself.

**What changes:**

| Area | Change | Size |
|---|---|---|
| `Web` — MCP endpoint + tools | New `MicroBurstTools` class (5 thin methods), MCP + JWT + MCP-metadata DI wiring | Small |
| `Web` — OpenIddict | Register stores, configure `/authorize` + `/token`, server metadata, DCR, wire `/login` as consent UI | **Largest piece** |
| EF migration | One migration for OpenIddict tables | Small |
| `Core` / `Infrastructure` | **None** | — |
| `Client` (WASM SPA) | **None** | — |
| REST API (`/api`) | **None** (untouched; coexists) | — |
| Infra / deployment | None (existing HTTPS + Postgres suffice) | — |

**What makes it cheap:** services already take `int userId`, so tools are a thin third
caller; the SDK handles MCP transport + discovery + the 401 challenge; existing TLS and
Identity login page are reused.

**What carries the risk:** correctly configuring the OAuth 2.1 server (metadata, PKCE, DCR,
audience/issuer validation) so Claude's discovery + token exchange succeed end-to-end.
This is standards-plumbing, fiddly to get exactly right, but a well-trodden path with
OpenIddict.

**Rough sequencing (build order, if pursued):**

1. **Resource server + read tools** against a hand-issued dev JWT — proves transport + tool
   layer. *(Smallest, validates the SDK integration.)*
2. **OpenIddict authorization server** — `/authorize`, `/token`, metadata, PKCE,
   `/login` as consent UI; JWT validation end-to-end with a manually-registered client.
   *(Largest.)*
3. **Dynamic Client Registration** + protected-resource-metadata polish → Claude connects
   with zero manual setup.
4. *(Future, optional)* mutating tools behind an `mcp:write` scope.

---

## 9. Open questions

- **CIMD vs DCR.** Claude prefers Client ID Metadata Documents (CIMD) over DCR for
  high-traffic servers; for a single-tenant personal app, DCR is sufficient. Confirm
  OpenIddict's DCR support meets Claude's expectations during milestone 3.
- **Consent UX.** Do we want a real consent screen, or auto-consent for first-party
  (our own) connector usage? Affects the OpenIddict authorize-endpoint handler.
- **Token lifetime.** Access-token TTL + refresh-token rotation policy for an assistant
  that may be idle for long stretches.

---

## 10. References

- MCP C# SDK (auth): <https://den.dev/blog/mcp-csharp-sdk-authorization/> ·
  SDK repo: <https://github.com/modelcontextprotocol/csharp-sdk> (`samples/ProtectedMcpServer`)
- Claude connector authentication: <https://claude.com/docs/connectors/building/authentication>
- Custom connectors / remote MCP: <https://support.claude.com/en/articles/11175166-get-started-with-custom-connectors-using-remote-mcp>
- OpenIddict: <https://documentation.openiddict.com/>
- Specs: OAuth 2.1, PKCE (RFC 7636), Protected Resource Metadata (RFC 9728),
  AS Metadata (RFC 8414), Dynamic Client Registration (RFC 7591),
  Resource Indicators (RFC 8707), Native App OAuth (RFC 8252)
