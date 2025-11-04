var builder = DistributedApplication.CreateBuilder(args);

// ============================================================================
// INFRASTRUCTURE LAYER: Datastores for Langfuse
// ============================================================================

// PostgreSQL - Primary datastore for Langfuse
var postgres = builder.AddPostgres("postgres")
    .WithEnvironment("POSTGRES_USER", "lfuser")
    .WithEnvironment("POSTGRES_PASSWORD", "lfpass")
    .WithEnvironment("POSTGRES_DB", "langfuse");

var langfuseDb = postgres.AddDatabase("langfuse-db", "langfuse");

// ClickHouse - Analytics datastore for Langfuse
var clickhouse = builder.AddContainer("clickhouse", "clickhouse/clickhouse-server", "24.8")
    .WithHttpEndpoint(port: 8123, targetPort: 8123, name: "http")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "tcp");

// Redis/Valkey - Caching layer
var redis = builder.AddRedis("redis");

// MinIO - S3-compatible storage for events and media
var minio = builder.AddContainer("minio", "minio/minio", "RELEASE.2024-12-07T00-00-00Z")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadminsecret")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "console")
    .WithArgs("server", "/data", "--console-address", ":9090");

// ============================================================================
// LANGFUSE SERVICES: Web API/UI and Background Worker
// ============================================================================

// Shared Langfuse configuration
var langfuseSecrets = new Dictionary<string, string>
{
    ["NEXTAUTH_SECRET"] = "CHANGEME_LONG_RANDOM_BASE64_SECRET_KEY_HERE",
    ["SALT"] = "CHANGEME_LONG_RANDOM_SALT_VALUE_HERE",
    ["ENCRYPTION_KEY"] = "CHANGEME_64_CHAR_HEX_ENCRYPTION_KEY_0123456789abcdef0123456789abc"
};

// Langfuse Web - API and UI
var langfuseWeb = builder.AddContainer("langfuse-web", "langfuse/langfuse", "latest")
    .WithEnvironment("PORT", "3000")
    .WithEnvironment("HOSTNAME", "0.0.0.0")
    .WithEnvironment("NEXTAUTH_URL", "http://langfuse-web:3000")
    .WithEnvironment("NEXTAUTH_SECRET", langfuseSecrets["NEXTAUTH_SECRET"])
    .WithEnvironment("SALT", langfuseSecrets["SALT"])
    .WithEnvironment("ENCRYPTION_KEY", langfuseSecrets["ENCRYPTION_KEY"])
    // Postgres connection
    .WithEnvironment("DATABASE_URL", $"postgresql://lfuser:lfpass@postgres:5432/langfuse?sslmode=disable")
    // ClickHouse connection
    .WithEnvironment("CLICKHOUSE_URL", "http://clickhouse:8123")
    .WithEnvironment("CLICKHOUSE_USER", "default")
    .WithEnvironment("CLICKHOUSE_PASSWORD", "")
    .WithEnvironment("CLICKHOUSE_DB", "default")
    .WithEnvironment("CLICKHOUSE_MIGRATION_URL", "clickhouse://clickhouse:9000")
    .WithEnvironment("CLICKHOUSE_CLUSTER_ENABLED", "false")
    // Redis connection
    .WithEnvironment("REDIS_CONNECTION_STRING", "redis://redis:6379")
    // S3/MinIO for events
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_BUCKET", "lf-events")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_ENDPOINT", "http://minio:9000")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_ACCESS_KEY_ID", "minioadmin")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_SECRET_ACCESS_KEY", "minioadminsecret")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_FORCE_PATH_STYLE", "true")
    // S3/MinIO for media
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_BUCKET", "lf-media")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_ENDPOINT", "http://minio:9000")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_ACCESS_KEY_ID", "minioadmin")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_SECRET_ACCESS_KEY", "minioadminsecret")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_FORCE_PATH_STYLE", "true")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http");
    // Note: Container resources don't need explicit WithReference calls;
    // Aspire manages container networking automatically

// Langfuse Worker - Background job processing
var langfuseWorker = builder.AddContainer("langfuse-worker", "langfuse/langfuse", "latest")
    .WithEnvironment("PORT", "3030")
    .WithEnvironment("HOSTNAME", "0.0.0.0")
    .WithEnvironment("NEXTAUTH_URL", "http://langfuse-web:3000")
    .WithEnvironment("NEXTAUTH_SECRET", langfuseSecrets["NEXTAUTH_SECRET"])
    .WithEnvironment("SALT", langfuseSecrets["SALT"])
    .WithEnvironment("ENCRYPTION_KEY", langfuseSecrets["ENCRYPTION_KEY"])
    .WithEnvironment("DATABASE_URL", $"postgresql://lfuser:lfpass@postgres:5432/langfuse?sslmode=disable")
    .WithEnvironment("CLICKHOUSE_URL", "http://clickhouse:8123")
    .WithEnvironment("CLICKHOUSE_USER", "default")
    .WithEnvironment("CLICKHOUSE_PASSWORD", "")
    .WithEnvironment("CLICKHOUSE_DB", "default")
    .WithEnvironment("CLICKHOUSE_MIGRATION_URL", "clickhouse://clickhouse:9000")
    .WithEnvironment("CLICKHOUSE_CLUSTER_ENABLED", "false")
    .WithEnvironment("REDIS_CONNECTION_STRING", "redis://redis:6379")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_BUCKET", "lf-events")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_ENDPOINT", "http://minio:9000")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_ACCESS_KEY_ID", "minioadmin")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_SECRET_ACCESS_KEY", "minioadminsecret")
    .WithEnvironment("LANGFUSE_S3_EVENT_UPLOAD_FORCE_PATH_STYLE", "true")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_BUCKET", "lf-media")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_ENDPOINT", "http://minio:9000")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_ACCESS_KEY_ID", "minioadmin")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_SECRET_ACCESS_KEY", "minioadminsecret")
    .WithEnvironment("LANGFUSE_S3_MEDIA_UPLOAD_FORCE_PATH_STYLE", "true");
    // Note: Container resources share the same network automatically

// ============================================================================
// APPLICATION SERVICES
// ============================================================================

// .NET Agent Service - Fetches prompts from Langfuse
var agent = builder.AddProject<Projects.AgenticStructuredOutput>("agent")
    .WithEnvironment("LANGFUSE_BASE_URL", "http://langfuse-web:3000")
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", "pk-lf-DEV")
    .WithEnvironment("LANGFUSE_SECRET_KEY", "sk-lf-DEV");

// Python Prompt Optimizer - Uses APO to optimize and stores in Langfuse
var optimizer = builder.AddContainer("optimizer", "optimizer-image", "latest")
    .WithEnvironment("LANGFUSE_BASE_URL", "http://langfuse-web:3000")
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", "pk-lf-DEV")
    .WithEnvironment("LANGFUSE_SECRET_KEY", "sk-lf-DEV")
    .WithHttpEndpoint(port: 8000, targetPort: 8000, name: "http");

builder.Build().Run();


