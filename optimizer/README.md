# Prompt Optimizer Service

Python FastAPI service that optimizes prompts using Agent Lightning Framework and APO (Automatic Prompt Optimization), storing versioned prompts in Langfuse.

## Overview

This service provides:
- **APO-based prompt optimization**: Iterative improvement of prompts based on objectives
- **Rollout management**: Staged deployment (canary → staging → production)
- **Langfuse integration**: Version control and labeling for prompts
- **REST API**: Simple HTTP endpoints for optimization and retrieval

## Architecture

```
Optimizer Service (Python/FastAPI)
    ↓
Agent Lightning Framework → APO optimization
    ↓
Langfuse API → Store versioned prompts with labels
```

## Endpoints

### POST /optimize
Optimize a prompt and store the result in Langfuse.

**Request:**
```json
{
  "name": "agent/system-prompt",
  "current_prompt": "You are a helpful assistant.",
  "draft": {
    "type": "text",
    "prompt": "You are a helpful agent for {{domain}}.",
    "config": {
      "temperature": 0.7,
      "max_tokens": 1000
    }
  },
  "objective": "improve clarity and reduce tokens",
  "labels": ["staging"],
  "commit_message": "Optimized for clarity",
  "rollout": {
    "strategy": "gradual",
    "percentage": 10,
    "evaluation_metric": "success_rate",
    "threshold": 0.95
  }
}
```

**Response:**
```json
{
  "version": 2,
  "labels": ["staging"],
  "optimized_prompt": "You are a helpful agent for {{domain}}.\n\nProvide clear, structured responses.",
  "rollout_plan": {
    "strategy": "gradual",
    "phases": [
      {"percentage": 10, "duration": "1h", "labels": ["canary"]},
      {"percentage": 50, "duration": "4h", "labels": ["staging"]},
      {"percentage": 100, "duration": "stable", "labels": ["staging"]}
    ]
  },
  "metrics": {
    "token_reduction": 0.15,
    "clarity_score": 0.92
  }
}
```

### GET /prompts/{name}
Retrieve a prompt by name and label.

**Query Parameters:**
- `label` (optional): Version label (default: "production")

### POST /rollout/{name}/promote
Promote a prompt from one label to another (e.g., staging → production).

**Query Parameters:**
- `from_label`: Source label (default: "staging")
- `to_label`: Target label (default: "production")

## Environment Variables

- `LANGFUSE_BASE_URL`: Langfuse server URL (default: http://langfuse-web:3000)
- `LANGFUSE_PUBLIC_KEY`: Langfuse public API key
- `LANGFUSE_SECRET_KEY`: Langfuse secret API key

## Running Locally

### With Docker
```bash
docker build -t optimizer-image:latest .
docker run -p 8000:8000 \
  -e LANGFUSE_BASE_URL=http://localhost:3000 \
  -e LANGFUSE_PUBLIC_KEY=pk-lf-... \
  -e LANGFUSE_SECRET_KEY=sk-lf-... \
  optimizer-image:latest
```

### With Python
```bash
pip install -r requirements.txt
export LANGFUSE_BASE_URL=http://localhost:3000
export LANGFUSE_PUBLIC_KEY=pk-lf-...
export LANGFUSE_SECRET_KEY=sk-lf-...
uvicorn optimizer.api:app --host 0.0.0.0 --port 8000
```

## Integration with .NET Aspire

This service is orchestrated by the AppHost project in the parent solution. Aspire handles:
- Service discovery
- Environment variable injection
- Container networking
- Health checks

## APO Optimization Flow

1. **Receive optimization request** with objective
2. **Define evaluation criteria** based on objective
3. **Run APO iterations** to improve prompt
4. **Generate rollout plan** for staged deployment
5. **Store optimized version** in Langfuse with labels
6. **Return metrics** and rollout information

## Dependencies

- `fastapi`: Web framework
- `uvicorn`: ASGI server
- `langfuse`: Langfuse Python SDK
- `agentic-lightning`: Agent Lightning Framework
- `pydantic`: Data validation
- `httpx`: HTTP client

## License

See parent project LICENSE.
