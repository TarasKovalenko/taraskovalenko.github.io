# Secure Refund Agent with .NET and Azure OpenAI

A runnable companion project for the article **Threat Modeling an AI Agent:
Prompt Injection Is Only the First Attack Vector**.

The sample demonstrates a security boundary around an LLM-based refund agent:

- Azure OpenAI authentication through Microsoft Entra ID;
- provider-neutral `IChatClient`;
- Microsoft Agent Framework function tools;
- parameter-bound human approval before writes;
- tenant, user, scope, and domain validation inside the application layer;
- an idempotent in-memory refund gateway;
- deterministic tests that do not call Azure.

## Architecture

```text
User prompt
    |
    v
Azure OpenAI / ChatClientAgent
    |
    +--> PreviewRefundAsync (read-only)
    |
    +--> ConfirmRefundAsync
             |
             v
        Human approval
             |
             v
    Scope + tenant + user + amount validation
             |
             v
        Idempotent refund gateway
```

The model proposes operations. It does not authorize them.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- an Azure OpenAI resource and chat-model deployment;
- Azure CLI authentication for local development, or managed identity in Azure.

The identity requires inference access to the target Azure OpenAI resource. Do
not grant the application broader roles than it needs.

## Configure local authentication

Sign in:

```bash
az login
```

Set the environment variables:

```bash
export AZURE_OPENAI_ENDPOINT="https://YOUR-RESOURCE.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT="YOUR-CHAT-DEPLOYMENT"
export AZURE_CREDENTIAL_MODE="azure-cli"
```

`.env.example` documents the same values. The program intentionally does not
load `.env` files automatically, which prevents accidental secret/configuration
loading in production.

For an Azure-hosted process, set:

```bash
export AZURE_CREDENTIAL_MODE="managed-identity"
```

The sample uses a system-assigned managed identity in this mode.

## Build and test

```bash
dotnet restore AiAgentThreatModel.slnx
dotnet build AiAgentThreatModel.slnx --no-restore
dotnet test AiAgentThreatModel.slnx --no-build
```

The tests exercise authorization and domain controls without requiring Azure
credentials or a model deployment.

## Run

Interactive mode:

```bash
dotnet run --project src/RefundAgent
```

Or pass the prompt directly:

```bash
dotnet run --project src/RefundAgent -- \
  "Preview a 25 USD refund for order 3d6d90b8-c11b-43a1-a690-540a962daeb0."
```

The in-memory sample order is:

| Field | Value |
|---|---|
| Order ID | `3d6d90b8-c11b-43a1-a690-540a962daeb0` |
| Tenant | `tenant-demo` |
| User | `user-demo` |
| Refundable balance | `250.00 USD` |

If the model requests `ConfirmRefundAsync`, the console displays the exact tool
name and arguments. Only `y` approves the operation; every other response
rejects it.

## Security properties

1. `tenantId`, `userId`, scopes, and correlation ID come from trusted execution
   context, not model arguments.
2. Repository lookup enforces tenant and user ownership.
3. Both preview and confirmation validate the refundable amount.
4. Confirmation requires the `refunds.write` scope.
5. `ApprovalRequiredAIFunction` pauses a state-changing call.
6. Approval displays the actual tool arguments rather than a model summary.
7. A normalized SHA-256 idempotency key prevents duplicate demo refunds.

The in-memory repository and gateway are teaching implementations. Replace them
with data and payment adapters that enforce the same invariants transactionally.
For a distributed production workflow, persist the approval request with its
correlation ID, expiry, argument hash, approver identity, and replay state.

## Project layout

```text
src/RefundAgent/
  Agent/           Agent construction and approval protocol
  Domain/          Execution context, contracts, and refund invariants
  Infrastructure/  In-memory adapters and sample data
  Program.cs       Azure client and console entry point
tests/
  RefundAgent.Tests/
```

## License

[MIT](LICENSE)
