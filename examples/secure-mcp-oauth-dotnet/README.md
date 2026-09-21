# Secure MCP OAuth resource server on ASP.NET Core

A runnable companion project for the article **Securing an MCP Server on
ASP.NET Core: OAuth 2.1, Scopes, and Token Audience**. It targets .NET 10 and
the official MCP C# SDK 2.0.0.

It demonstrates the resource-server side of the architecture:

- OAuth Protected Resource Metadata (RFC 9728)
- JWT signature, issuer, lifetime, and exact audience validation
- per-tool scopes with an HTTP `403 insufficient_scope` step-up challenge
- a second per-client entitlement check even when a token contains the scope
- tenant identity derived from the validated token, not tool arguments
- audit events without tokens or tool arguments
- rate limiting partitioned by `client_id` and `sub`
- integration tests for the security boundaries

It deliberately does **not** implement an authorization server. In production, use an established IdP rather than issuing tokens in this API. Tests mint short-lived symmetric tokens only inside the `Testing` environment.

## Authorization server contract

Configure the IdP before running the server:

| Setting | `finance-desktop` | `finance-admin` |
|---|---|---|
| Client type | Public/native | Public/native or confidential web client |
| Grant | Authorization code | Authorization code |
| PKCE | Required, `S256` | Required, `S256` |
| Redirect URIs | Exact registered URIs | Exact registered URIs |
| Allowed scopes | `expenses.read` | `expenses.read`, `expenses.approve` |
| Consent | Per user and client | Per user and client; approve scope shown separately |

The authorization server must:

1. accept `resource=https://expenses.example.com/mcp` in both authorization and token requests;
2. bind the grant to the user, client, requested resource, redirect URI, PKCE challenge, and approved scopes;
3. issue an access token whose audience is exactly `https://expenses.example.com/mcp`;
4. include `iss`, `aud`, `exp`, `iat`, `sub`, `client_id` (or `azp`), `scope`, `tenant_id`, and preferably `jti`;
5. refuse `expenses.approve` for `finance-desktop`, even if a user asks for it;
6. store and revoke consent per user/client instead of treating one approval as global consent.

The exact admin-console names vary by provider. These are protocol and policy requirements, not a claim that every IdP exposes identical switches.

## Run against a real IdP

Replace the example authority, issuer, audience, and client entitlement configuration in `src/ExpenseMcp/appsettings.json`, then:

```bash
dotnet run --project src/ExpenseMcp/ExpenseMcp.csproj
```

The public metadata document is available at the configured absolute URL, whose path is:

```text
/.well-known/oauth-protected-resource
```

Signing keys are discovered from `Authentication:Authority` over HTTPS. That is the only
mode outside `Development` and `Testing`.

## Run locally without an IdP

`appsettings.Development.json` points the server at a local resource identifier and supplies
`Authentication:LocalSigningKey`. When that key is present *and* the environment is
`Development`, the JWT handler validates HS256 tokens against it instead of fetching JWKS.
Production never reaches that branch: see `ResolveLocalSigningKey` in `src/ExpenseMcp/Program.cs`.

Start the server:

```bash
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/ExpenseMcp --urls http://localhost:5099
```

`tools/DevToken` stands in for the authorization server and prints one access token. It is a
separate console app on purpose - the resource server must never issue tokens:

```bash
# read-write client
TOKEN=$(dotnet run --project tools/DevToken -- --client finance-admin)

# read-only client
TOKEN=$(dotnet run --project tools/DevToken -- --client finance-desktop --scope expenses.read)

# token for the wrong resource, to watch audience validation reject it
TOKEN=$(dotnet run --project tools/DevToken -- --audience https://files.example.com/mcp)
```

Options: `--client`, `--scope`, `--sub`, `--tenant`, `--audience`, `--minutes`, `--settings`.
Run `dotnet run --project tools/DevToken -- --help` for defaults.

### Try the boundaries by hand

Discovery works without a token:

```bash
curl -s http://localhost:5099/.well-known/oauth-protected-resource

curl -si -X POST http://localhost:5099/mcp \
  -H 'Content-Type: application/json' -d '{}' | grep -i www-authenticate
# WWW-Authenticate: Bearer resource_metadata="http://localhost:5099/.well-known/oauth-protected-resource", scope="expenses.read"
```

A tool call needs the protocol headers and request-scoped `_meta` that SDK 2.0.0 expects:

```bash
call_tool() {
  curl -si -X POST http://localhost:5099/mcp \
    -H "Authorization: Bearer $TOKEN" \
    -H 'Content-Type: application/json' \
    -H 'Accept: application/json, text/event-stream' \
    -H 'MCP-Protocol-Version: 2026-07-28' \
    -H 'Mcp-Method: tools/call' \
    -H "Mcp-Name: $1" \
    -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"$1\",\"arguments\":$2,\"_meta\":{\"io.modelcontextprotocol/protocolVersion\":\"2026-07-28\",\"io.modelcontextprotocol/clientInfo\":{\"name\":\"curl\",\"version\":\"1.0\"},\"io.modelcontextprotocol/clientCapabilities\":{}}}}"
}

call_tool list_expenses '{}'
call_tool approve_expense '{"expenseId":"7fce98d1-d91e-44b0-aab7-440af78d18af"}'
```

What each token produces:

| Token | `list_expenses` | `approve_expense` |
|---|---|---|
| `finance-admin` | `200` | `200`, status becomes `Approved` |
| `finance-desktop --scope expenses.read` | `200` | `403 insufficient_scope, scope="expenses.approve"` |
| `--client finance-desktop --scope "expenses.read expenses.approve"` | `200` | `403`, the client entitlement rejects the scope |
| `--audience https://files.example.com/mcp` | `401` | `401` |
| any token, unmapped tool name | - | `403`, fail-closed default |

Audit events and deny reasons go to stdout as JSON (`EventId` 4100 for audit, 4001 and 4002
for security). The audit event carries the subject, client, tool, outcome, and status, and no
token or tool arguments.

The dev signing key in `appsettings.Development.json` is a committed placeholder. It is only
usable in `Development`, but do not reuse the value anywhere else.

## Test

```bash
dotnet test SecureMcpOAuth.slnx
```

The eight tests cover:

- canonical protected-resource metadata
- a `401` discovery challenge for a missing token
- rejection of a token for another audience
- a `403` step-up challenge for a missing tool scope
- fail-closed behavior for a tool without a policy mapping
- rejection when a client uses a scope outside its entitlement
- an authorized tool call and redacted audit event
- rate limiting with `Retry-After`

## Production notes

- Put TLS termination and forwarded-header validation in the deployment design. The generated discovery URL must not be derived from untrusted forwarding headers.
- Replace `LoggerAuditSink` with a durable, access-controlled append-only destination. Logging is not durable auditing by itself.
- The built-in limiter is process-local. Use a gateway or distributed limiter when limits must be global across replicas.
- Keep the MCP endpoint stateless unless you need server-to-client capabilities that require state.
- Re-check authorization inside application code before every side effect. Middleware provides the HTTP step-up response; the tool guard prevents a future alternate route from bypassing the decision.
- Never forward the incoming MCP token to a downstream API. Obtain a token for that downstream audience through an appropriate delegation flow.

## References

- [MCP Authorization 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization)
- [RFC 9728: OAuth Protected Resource Metadata](https://www.rfc-editor.org/rfc/rfc9728.html)
- [RFC 8707: Resource Indicators](https://www.rfc-editor.org/rfc/rfc8707.html)
- [RFC 9700: OAuth Security Best Current Practice](https://www.rfc-editor.org/rfc/rfc9700.html)
- [Official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
