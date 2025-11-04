# Quick Start Guide

Get the entire AgenticStructuredOutput system running in 5 minutes.

## Prerequisites

- .NET 9.0 SDK
- Docker and Docker Compose
- GitHub token (for AI model access)

## Step 1: Clone and Build

```bash
# Clone the repository
git clone https://github.com/Tyler-R-Kendrick/AgenticStructuredOutput.git
cd AgenticStructuredOutput

# Build the solution
dotnet build
```

## Step 2: Build Optimizer Image

```bash
cd optimizer
docker build -t optimizer-image:latest .
cd ..
```

## Step 3: Set Environment Variables

```bash
# For GitHub Models (recommended)
export GITHUB_TOKEN="ghp_your_github_token_here"

# OR for OpenAI
export OPENAI_API_KEY="sk-your_openai_key_here"

# Langfuse keys (will be generated after first run)
export LANGFUSE_PUBLIC_KEY="pk-lf-..."
export LANGFUSE_SECRET_KEY="sk-lf-..."
```

## Step 4: Run with Aspire

```bash
cd AppHost
dotnet run
```

This starts:
- ✅ PostgreSQL (port 5432)
- ✅ ClickHouse (ports 8123, 9000)
- ✅ Redis (port 6379)
- ✅ MinIO (ports 9000, 9090)
- ✅ Langfuse Web UI (port 3000)
- ✅ Langfuse Worker
- ✅ .NET Agent (port 5000)
- ✅ Python Optimizer (port 8000)

## Step 5: Access Services

### Aspire Dashboard
- **URL**: http://localhost:15000 (typically)
- **Purpose**: Monitor all services, view logs, check health

### Langfuse UI
- **URL**: http://localhost:3000
- **First Time**:
  1. Create an account
  2. Create a project
  3. Generate API keys (Settings → API Keys)
  4. Update environment variables with your keys

### .NET Agent API
- **URL**: http://localhost:5000
- **Test**: `curl http://localhost:5000/health`

### Python Optimizer API
- **URL**: http://localhost:8000
- **Docs**: http://localhost:8000/docs (Swagger UI)
- **Test**: `curl http://localhost:8000/health`

## Step 6: Test the System

### A. Test Agent Directly (without Langfuse)

```bash
curl -X POST http://localhost:5000/agent \
  -H "Content-Type: application/json" \
  -d '{
    "input": "{\"fullName\":\"Jane Smith\",\"yearsOld\":25}",
    "schema": "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"age\":{\"type\":\"integer\"}}}"
  }'
```

Expected response:
```json
{
  "name": "Jane Smith",
  "age": 25
}
```

### B. Create a Prompt in Langfuse

1. Go to http://localhost:3000
2. Create a new prompt:
   - **Name**: `agent/system-prompt`
   - **Type**: Text
   - **Content**: `You are an expert data mapping agent.`
   - **Labels**: `production`
   - **Config**: `{"temperature": 0.7}`

### C. Optimize the Prompt

```bash
curl -X POST http://localhost:8000/optimize \
  -H "Content-Type: application/json" \
  -d '{
    "name": "agent/system-prompt",
    "draft": {
      "type": "text",
      "prompt": "You are an expert data mapping agent for {{domain}}.",
      "config": {"temperature": 0.7}
    },
    "objective": "improve clarity",
    "labels": ["staging"],
    "commit_message": "Improved clarity"
  }'
```

### D. Promote to Production

```bash
curl -X POST http://localhost:8000/rollout/agent/system-prompt/promote \
  ?from_label=staging&to_label=production
```

## Common Issues

### Port Conflicts
If ports are already in use, modify them in `AppHost/AppHost.cs`.

### Docker Not Running
```bash
# Check Docker status
docker info

# Start Docker Desktop or daemon
sudo systemctl start docker  # Linux
```

### Langfuse Not Starting
- Check all infrastructure containers are healthy (PostgreSQL, ClickHouse, Redis, MinIO)
- View logs in Aspire dashboard
- Ensure ports are not blocked by firewall

### Agent Can't Access GitHub Models
- Verify `GITHUB_TOKEN` is set correctly
- Check token has required permissions
- Try with `OPENAI_API_KEY` as alternative

### Optimizer Image Not Found
```bash
# Rebuild the optimizer image
cd optimizer
docker build -t optimizer-image:latest .
```

## Next Steps

1. **Read Full Documentation**
   - [AppHost/README.md](AppHost/README.md) - Complete Aspire guide
   - [optimizer/README.md](optimizer/README.md) - Optimizer details
   - [Main README](README.md) - Architecture overview

2. **Explore Aspire Dashboard**
   - View service health and logs
   - Monitor resource usage
   - Check OpenTelemetry traces

3. **Experiment with Prompts**
   - Create multiple versions
   - Test A/B deployments
   - Use labels for staged rollouts

4. **Customize Configuration**
   - Update agent instructions
   - Modify schema validation
   - Configure optimization objectives

## Production Deployment

For production, consider:
- Use managed services (Azure PostgreSQL, Redis, etc.)
- Enable TLS/SSL for all connections
- Use Azure Key Vault or similar for secrets
- Set up monitoring and alerting
- Configure backup and disaster recovery
- Use Azure Container Apps or Kubernetes for hosting

## Support

- **Issues**: https://github.com/Tyler-R-Kendrick/AgenticStructuredOutput/issues
- **Discussions**: https://github.com/Tyler-R-Kendrick/AgenticStructuredOutput/discussions
- **Docs**: See individual README files in each component directory

---

**Happy Building! 🚀**
