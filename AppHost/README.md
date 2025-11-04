# AppHost - .NET Aspire Orchestration

.NET Aspire AppHost project that orchestrates the entire AgenticStructuredOutput system with Langfuse integration for runtime prompt management.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      .NET Aspire AppHost                        │
│                  (Service Discovery & Orchestration)            │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
┌───────▼────────┐   ┌────────▼──────────┐   ┌────▼───────────┐
│  .NET Agent    │   │  Python Optimizer │   │ Langfuse Stack │
│   (C#/ASP.NET) │   │   (FastAPI/APO)   │   │   (Containers) │
└────────────────┘   └───────────────────┘   └────────────────┘
        │                     │                       │
        │  Fetch prompts      │  Store versions       │
        └────────────┬────────┴───────────┬───────────┘
                     │                    │
                ┌────▼────────────────────▼────┐
                │      Langfuse Web API        │
                │    (Prompt Management)       │
                └──────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
┌───────▼────────┐   ┌────────▼──────────┐   ┌────▼───────────┐
│   PostgreSQL   │   │   ClickHouse      │   │    Redis       │
│  (Primary DB)  │   │   (Analytics)     │   │   (Cache)      │
└────────────────┘   └───────────────────┘   └────────────────┘
                              │
                     ┌────────▼──────────┐
                     │      MinIO        │
                     │   (S3 Storage)    │
                     └───────────────────┘
```

## Components

### Infrastructure Layer (Langfuse Dependencies)

#### PostgreSQL
- **Purpose**: Primary database for Langfuse
- **Configuration**:
  - User: `lfuser`
  - Password: `lfpass`
  - Database: `langfuse`
- **Port**: 5432

#### ClickHouse
- **Purpose**: Analytics and observability data
- **Ports**:
  - HTTP: 8123
  - Native TCP: 9000

#### Redis
- **Purpose**: Caching and session management
- **Port**: 6379

#### MinIO
- **Purpose**: S3-compatible storage for events and media
- **Configuration**:
  - Access Key: `minioadmin`
  - Secret Key: `minioadminsecret`
- **Ports**:
  - API: 9000
  - Console: 9090
- **Buckets**:
  - `lf-events`: Event data
  - `lf-media`: Media uploads

### Langfuse Services

#### Langfuse Web
- **Purpose**: Web UI and public API
- **Port**: 3000
- **Features**:
  - Prompt versioning and management
  - Label-based deployment (staging, production, etc.)
  - Public API for reading/writing prompts
  - Web dashboard for monitoring

#### Langfuse Worker
- **Purpose**: Background job processing
- **Port**: 3030
- **Features**:
  - Async data processing
  - Analytics computation
  - Scheduled tasks

### Application Services

#### .NET Agent Service
- **Project**: `AgenticStructuredOutput`
- **Purpose**: AI agent that fetches prompts from Langfuse at runtime
- **Key Features**:
  - Fetches prompts by name + label
  - No code redeployment for prompt updates
  - Microsoft Agent Framework integration
  - Azure AI Inference (GitHub Models)

#### Python Optimizer Service
- **Directory**: `optimizer/`
- **Purpose**: APO-based prompt optimization with rollout management
- **Key Features**:
  - Agent Lightning Framework integration
  - APO (Automatic Prompt Optimization)
  - Staged rollout strategies
  - Stores optimized versions in Langfuse

## Running the System

### Prerequisites
- .NET 9.0 SDK
- Docker (for containerized services)
- GitHub token or OpenAI API key (for the .NET agent)

### Build the Optimizer Image
```bash
cd optimizer
docker build -t optimizer-image:latest .
cd ..
```

### Run with Aspire
```bash
cd AppHost
dotnet run
```

This will:
1. Start all infrastructure containers (PostgreSQL, ClickHouse, Redis, MinIO)
2. Start Langfuse web and worker services
3. Start the .NET agent service
4. Start the Python optimizer service
5. Open the Aspire dashboard (typically http://localhost:15000)

### Initial Setup

1. **Access Langfuse UI**: Navigate to http://localhost:3000
2. **Create a project**: Set up your first Langfuse project
3. **Generate API keys**: Create public/secret key pair
4. **Update environment variables**: Set `LANGFUSE_PUBLIC_KEY` and `LANGFUSE_SECRET_KEY`

### Environment Variables

The following environment variables are injected by Aspire:

#### For .NET Agent
- `LANGFUSE_BASE_URL`: Langfuse web service URL
- `LANGFUSE_PUBLIC_KEY`: Public API key (pk-lf-...)
- `LANGFUSE_SECRET_KEY`: Secret API key (sk-lf-...)
- `GITHUB_TOKEN`: For GitHub Models access

#### For Python Optimizer
- `LANGFUSE_BASE_URL`: Langfuse web service URL
- `LANGFUSE_PUBLIC_KEY`: Public API key
- `LANGFUSE_SECRET_KEY`: Secret API key

## Usage Workflow

### 1. Create Initial Prompt
Use the Langfuse UI or API to create an initial prompt:

```bash
curl -X POST http://localhost:3000/api/public/v2/prompts \
  -H "Authorization: Basic $(echo -n 'pk-lf-...:sk-lf-...' | base64)" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "agent/system-prompt",
    "type": "text",
    "prompt": "You are a helpful assistant for {{domain}}.",
    "labels": ["production"],
    "config": {"temperature": 0.7}
  }'
```

### 2. Optimize Prompt
Use the Python optimizer to improve the prompt:

```bash
curl -X POST http://localhost:8000/optimize \
  -H "Content-Type: application/json" \
  -d '{
    "name": "agent/system-prompt",
    "draft": {
      "type": "text",
      "prompt": "You are a helpful agent for {{domain}}.",
      "config": {"temperature": 0.7}
    },
    "objective": "improve clarity and reduce tokens",
    "labels": ["staging"],
    "commit_message": "APO optimization for clarity",
    "rollout": {
      "strategy": "gradual",
      "percentage": 10,
      "evaluation_metric": "success_rate",
      "threshold": 0.95
    }
  }'
```

### 3. Test with Staging Label
The .NET agent can fetch the staging version:

```bash
curl http://localhost:5000/agent \
  -H "Content-Type: application/json" \
  -d '{
    "input": "{\"fullName\":\"John Doe\"}",
    "schema": "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}"
  }'
```

### 4. Promote to Production
Once validated, promote to production:

```bash
curl -X POST http://localhost:8000/rollout/agent/system-prompt/promote?from_label=staging&to_label=production
```

## Aspire Benefits

### Service Discovery
- Automatic DNS resolution between services
- No hardcoded hostnames or IPs

### Centralized Logging
- OpenTelemetry integration
- Unified log viewing in Aspire dashboard

### Health Checks
- Automatic health monitoring
- Service dependency visualization

### Secrets Management
- Secure configuration injection
- User secrets support

### Development Dashboard
- Real-time service status
- Resource utilization
- Logs and traces
- Environment variables

## Configuration

### Secrets
Update the following in `AppHost.cs` before production deployment:
- `NEXTAUTH_SECRET`: Random base64 string (32+ chars)
- `SALT`: Random base64 string (32+ chars)
- `ENCRYPTION_KEY`: 64-character hex string
- Langfuse API keys

### Production Considerations
- Use Azure Key Vault or equivalent for secrets
- Enable TLS for all services
- Configure proper network policies
- Set up monitoring and alerting
- Use managed services (Azure PostgreSQL, Redis, etc.)

## Troubleshooting

### Services Not Starting
- Check Docker is running
- Verify port availability
- Review Aspire dashboard for errors

### Langfuse Connection Issues
- Ensure all infrastructure services are healthy
- Check DATABASE_URL format
- Verify ClickHouse and Redis connectivity

### Agent Can't Fetch Prompts
- Confirm Langfuse API keys are set
- Verify prompt exists with the specified label
- Check network connectivity between services

## Further Reading

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Langfuse Documentation](https://langfuse.com/docs)
- [Agent Lightning Framework](https://github.com/microsoft/agent-lightning)
- [Microsoft Agent Framework](https://github.com/microsoft/agents)

## License

See parent project LICENSE.
