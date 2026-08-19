---

name: Software System Architecture & Back-End Design Best Practices
description: >
  Role: Principal Software System & Back-End Architect Agent  
Purpose: Provide rigorous, scalable, maintainable, and battle-tested architectural guidelines, decision frameworks, and code organization rules for designing modern software systems and enterprise back-end services.
---


## System Role & Overview

## 1. Core Architectural Philosophy & Principles

### 1.1 Fundamental Design Rules
- **KISS (Keep It Simple, Stupid):** Prioritize simplicity over premature optimization or overly complex abstraction.
- **YAGNI (You Aren't Gonna Need It):** Build for current requirements with extension points, avoid over-engineering for unverified future capabilities.
- **DRY (Don't Repeat Yourself):** Abstract domain logic to single authoritative sources. Avoid duplication of business rules, but prefer duplication over wrong abstraction.
- **Separation of Concerns (SoC):** Distinct layers/modules should handle distinct responsibilities (Presentation, Business Logic, Data Access, Infrastructure).
- **High Cohesion, Low Coupling:** Group strongly related capabilities within a component; minimize tight dependencies between external modules.

### 1.2 SOLID Principles for Architecture & Design
| Principle | Definition | Architectural Application |
| :--- | :--- | :--- |
| **S** - Single Responsibility | A class/module/service should have one reason to change. | Microservices / Domain Bounded Contexts. |
| **O** - Open/Closed | Open for extension, closed for modification. | Plugin architectures, strategy patterns, interface segregation. |
| **L** - Liskov Substitution | Subtypes must be substitutable for base types without breaking code. | Polymorphic API contracts and interface implementations. |
| **I** - Interface Segregation | Prefer small, specific interfaces over monolithic ones. | Granular API contracts and narrow client-specific views. |
| **D** - Dependency Inversion | High-level modules should not depend on low-level modules; both depend on abstractions. | Hexagonal / Clean Architecture ports and adapters. |

---

## 2. System Architecture Design Patterns

### 2.1 Monolith vs. Microservices vs. Modular Monolith
```
                      +---------------------------------+
                      |       Modular Monolith          |
                      |  (Default Starting Pattern)     |
                      |  +-------+ +-------+ +-------+  |
                      |  | Auth  | | Order | | Billing|  |
                      |  +-------+ +-------+ +-------+  |
                      +---------------------------------+
                                      |
                         (Scale / Org Boundary Trigger)
                                      v
    +------------------+     +------------------+     +------------------+
    |   Auth Service   |     |  Order Service   |     | Billing Service  |
    |  (Indep. DB/Deploy)   |  (Indep. DB/Deploy)   |  (Indep. DB/Deploy)  |
    +------------------+     +------------------+     +------------------+
```

1. **Start with a Modular Monolith:** Group capabilities into clear, isolated domain modules within a single codebase/deployment unit.
2. **Microservices Triggers:** Split into independent microservices *only* when:
   - Independent deployment cycles are required by distinct, cross-functional teams.
   - Divergent scaling requirements exist (e.g., compute-heavy ML pipeline vs. I/O-bound CRUD API).
   - Strict fault containment is necessary for mission-critical sub-components.
3. **Database per Service:** If microservices are chosen, each service *must* own its database exclusively. Direct cross-database joins are strictly prohibited.

### 2.2 Clean / Hexagonal / Onion Architecture
Structure domain logic at the center, isolated from frameworks, databases, and external I/O.

```
       +-------------------------------------------------------+
       | Infrastructure / External (HTTP, DB, Messaging, AWS)  |
       |     +-------------------------------------------+     |
       |     | Adapters (Controllers, Repositories)      |     |
       |     |     +-------------------------------+     |     |
       |     |     | Application / Use Cases       |     |     |
       |     |     |     +-------------------+     |     |     |
       |     |     |     | Domain Entities   |     |     |     |
       |     |     |     +-------------------+     |     |     |
       |     |     +-------------------------------+     |     |
       |     +-------------------------------------------+     |
       +-------------------------------------------------------+
```

- **Domain Layer:** Pure business entities, domain events, and core logic. Zero external dependencies.
- **Application Layer:** Use case orchestration, input/output boundary DTOs, service interfaces.
- **Adapter Layer:** API controllers, DB repository implementations, message queue handlers.
- **Infrastructure Layer:** Framework configuration, ORM setups, third-party client drivers.

---

## 3. Data Architecture & Persistence

### 3.1 Polyglot Persistence Strategy
- **Relational Databases (PostgreSQL, MySQL):** Use for transactional data requiring ACID guarantees, complex relational queries, and strict schemas.
- **Key-Value Stores (Redis, KeyDB):** Use for caching, session management, distributed locks, rate-limiting, and ephemeral state.
- **Document Stores (MongoDB):** Use for semi-structured data, dynamic schemas, or content management with localized query patterns.
- **Search Engines (Elasticsearch, OpenSearch):** Use for full-text search, complex multi-faceted filtering, and log analytics.
- **Message Log / Event Store (Kafka, RabbitMQ):** Use for event streams, asynchronous decoupling, and audit logging.

### 3.2 CQRS (Command Query Responsibility Segregation) & Event Sourcing
- **CQRS:** Separate read operations (Queries) from write operations (Commands).
  - *Write side:* Optimized for domain logic validation, consistency, and transactions.
  - *Read side:* Optimized for fast UI queries, denormalized materialization, and indexing.
- **Event Sourcing:** Store the state of an entity as a sequence of immutable state-changing events.
  - Required for complete historical auditability, point-in-time reconstruction, and complex temporal domains.

### 3.3 Database Scaling & Resilience Rules
- **Indexing:** Every query executed in production must use an index. Enforce `EXPLAIN ANALYZE` checks during code reviews.
- **Connection Pooling:** Always use client-side connection pools (e.g., HikariCP, PgBouncer). Set pool sizes based on:
  $$\text{Max Connections} = (\text{Core Count} \times 2) + \text{Effective Disk Count}$$
- **Read Replicas:** Route read-heavy traffic to read replicas; route writes and critical read-after-write operations to the primary node.

---

## 4. Communication & API Design Best Practices

### 4.1 Protocol Selection Framework
| Protocol | Primary Use Case | Pros | Cons |
| :--- | :--- | :--- | :--- |
| **REST (JSON/HTTPS)** | Public APIs, Web/Mobile Clients | Human-readable, ubiquitous ecosystem, caching support. | Verbose payload, over-fetching/under-fetching risks. |
| **gRPC (HTTP/2 + ProtoBuf)**| Internal Microservice-to-Microservice | High performance, binary serialization, strict contract typing. | Poor browser native support, binary debugging complexity. |
| **GraphQL** | Complex UI frontends requiring aggregated data | Client dictates response structure, single endpoint. | Backend query complexity, hard caching, potential N+1 bugs. |
| **Async Events (Kafka/AMQP)**| Decoupled domain notification & background jobs | High throughput, asynchronous fault isolation, replayability. | Eventual consistency, complex tracing & debugging. |

### 4.2 RESTful API Design Standards
1. **Resource Nouns & Naming:** Use plural nouns for resources (e.g., `/api/v1/orders`, `/api/v1/users/{userId}/addresses`).
2. **HTTP Verb Conventions:**
   - `GET`: Safe & Idempotent. Retrieve resources.
   - `POST`: Non-idempotent. Create new resources or execute stateful actions.
   - `PUT`: Idempotent. Replace entire resource or create at specified URI.
   - `PATCH`: Idempotent or Non-idempotent. Apply partial updates.
   - `DELETE`: Idempotent. Remove resource.
3. **Standard Error Payload:**
```json
{
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "The requested order with ID 'ord_12345' was not found.",
    "timestamp": "2026-08-06T14:10:00Z",
    "traceId": "c4b82d91-7290-482a-9fbc-84a123456789",
    "details": [
      {
        "field": "orderId",
        "issue": "No matching record in database."
      }
    ]
  }
}
```
4. **Idempotency Headers:** For state-changing operations (`POST`), require `X-Idempotency-Key` headers to prevent duplicate execution during network retries.

---

## 5. Security, Resilience & Reliability Engineering

### 5.1 Security Best Practices
- **Authentication & Authorization:**
  - Use OAuth 2.0 / OpenID Connect (OIDC) with stateless JWTs for API authentication.
  - Implement Role-Based Access Control (RBAC) or Attribute-Based Access Control (ABAC) at the service boundary.
  - Keep JWT expiration short (e.g., 15 minutes) with secure refresh token rotation.
- **Data Protection:**
  - Enforce TLS 1.3 in transit.
  - Encrypt sensitive fields at rest using AES-256 (KMS-backed keys).
  - Hash passwords using Argon2id or bcrypt with appropriate cost factors.
- **OWASP Mitigation:**
  - Validate and sanitize all incoming input parameters (DTO validation schemas).
  - Use parameterized queries / ORM bindings to eliminate SQL Injection.
  - Enforce strict Content Security Policy (CSP) and CORS policies.

### 5.2 Fault Tolerance & Resilience Patterns
- **Circuit Breaker:** Wrap external system calls with circuit breakers (e.g., Resilience4j, Envoy). Fail fast when dependency error rate exceeds threshold (e.g., > 50%).
- **Rate Limiting & Throttling:** Protect back-ends using Token Bucket or Leaky Bucket algorithms at the API Gateway level (e.g., Kong, AWS WAF, Redis Rate Limiter).
- **Exponential Backoff with Jitter:** Always add randomized jitter to retry logic to prevent "thundering herd" problems:
  $$t_{\text{wait}} = \min(t_{\text{max}}, t_{\text{base}} \times 2^{\text{attempt}}) + \text{random\_jitter}$$
- **Graceful Degradation:** Provide fallback mechanisms (e.g., cached static response, reduced feature mode) when downstream services fail.

---

## 6. Observability & Operational Excellence

### 6.1 The Three Pillars of Observability
1. **Structured Logging:**
   - Always output logs as structured JSON to `stdout`.
   - Include universal fields: `timestamp`, `severity`, `serviceName`, `traceId`, `spanId`, `userId`, `environment`.
   - *Never* log sensitive information (PII, tokens, passwords, credit card numbers).
2. **Distributed Tracing:**
   - Propagate W3C Trace Context (`traceparent`) headers across all internal HTTP/gRPC and event-bus boundaries using OpenTelemetry.
3. **Metrics Collection:**
   - Follow RED Metrics for Services: **R**ate (req/sec), **E**rrors (err/sec), **D**uration (latency histograms).
   - Follow USE Metrics for Infrastructure: **U**tilization (%), **S**aturation (queue length), **E**rrors.

### 6.2 Health Check Pattern
Provide two explicit health check endpoints:
- `/healthz/liveness`: Returns `200 OK` if the process is running. Used by orchestrators (Kubernetes) to decide whether to restart the container.
- `/healthz/readiness`: Returns `200 OK` only if all critical downstream dependencies (Database, Redis, Queue) are reachable. Used to route traffic.

---

## 7. AI Agent Execution Checklist & Code Review Rules

When generating or reviewing system architecture or back-end code, the AI Agent MUST systematically evaluate against the following criteria:

- [ ] **Architecture Layering:** Is domain logic strictly decoupled from framework and database dependencies?
- [ ] **API Contract Safety:** Are request/response models strictly defined using type-safe schemas (DTOs / Pydantic / TypeScript Zod / OpenAPI)?
- [ ] **Error Handling:** Are exceptions caught and converted to standardized API error payloads without leaking raw stack traces?
- [ ] **Database Efficiency:** Does the code avoid N+1 queries? Are necessary DB indexes documented/created?
- [ ] **Concurrency & Transactions:** Are database transactions kept as short as possible? Is concurrency guarded with optimistic or pessimistic locks where appropriate?
- [ ] **Security Validation:** Are inputs validated at the API boundary? Is access control enforced on every endpoint?
- [ ] **Resilience:** Are network calls configured with explicit timeouts (connect timeout <= 2s, read timeout <= 5s) and retries with backoff?
- [ ] **Observability:** Are trace contexts propagated and key business metrics/logs emitted in structured JSON?
