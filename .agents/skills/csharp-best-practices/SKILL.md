---
name: csharp-best-practices
description: Enforces modern C# (.NET 8/9+) coding standards, asynchronous patterns, memory optimization, and clean architecture guidelines when writing or reviewing C# code.
---

# C# & .NET Coding Best Practices Skill

## Goal
Guide the agent to write, refactor, and review idiomatic, modern C# code that adheres to Microsoft design guidelines, SOLID principles, high-performance practices, and clean architecture.

---

## 1. Core Principles & Coding Standards

### Modern C# Language Features
* **Primary Constructors:** Prefer primary constructors for dependency injection in classes and record declarations.
* **Records for Immutability:** Use `record` or `record struct` for DTOs, messages, and immutable data containers.
* **Collection Expressions & Expressions:** Favor collection expressions `[1, 2, 3]` over `new List<int> { ... }`.
* **Pattern Matching:** Use pattern matching and `switch` expressions over nested `if/else` or standard `switch` statements.
* **Nullable Reference Types (NRT):** Always enforce `<Nullable>enable</Nullable>`. Use explicit annotations (`?`, `!`, `[NotNullWhen]`) instead of ignoring compiler warnings.

### Asynchronous Programming (`async`/`await`)
* **Async All the Way:** Avoid sync-over-async (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`).
* **Cancellation Tokens:** Always pass `CancellationToken` through asynchronous call stacks.
* **ValueTask Optimization:** Use `ValueTask` or `ValueTask<T>` on high-throughput hot paths where synchronous completion is frequent.
* **IAsyncEnumerable:** Use `IAsyncEnumerable<T>` for streaming collections asynchronously.

### Memory & Performance Optimization
* **Avoid Allocations on Hot Paths:** Use `ReadOnlySpan<T>`, `Span<T>`, and `Memory<T>` for slice operations instead of string allocation (`Substring`) or array slicing.
* **Memory Pooling:** Use `ArrayPool<T>.Shared` or `ObjectPool<T>` for temporary buffer allocations.
* **StringBuilder:** Use `StringBuilder` or interpolated string handlers for string concatenation inside loops.

---

## 2. Code Design Patterns

### Dependency Injection & Architecture
* **Constructor Injection:** Always inject dependencies via constructors. Avoid Service Locator pattern (`IServiceProvider.GetService`).
* **Service Lifetimes:** Tightly control lifetimes:
  * `Transient`: Lightweight stateless services.
  * `Scoped`: Services tied to request/unit-of-work scope.
  * `Singleton`: Thread-safe, stateless, or cached services across app lifecycle.
* **Keyed Services:** Leverage .NET 8+ `[FromKeyedServices]` when resolving multiple implementations of an interface.

### Error Handling & Control Flow
* **Exceptions for Exceptional Cases:** Do not use exceptions for expected control flow.
* **Result Pattern:** Consider `Result<T>` or `OneOf` library patterns for domain errors rather than throwing runtime exceptions.
* **Global Exception Middleware:** Prefer ASP.NET Core `IExceptionHandler` middleware for API exception handling over per-controller try-catch blocks.

---

## 3. Formatting & Naming Conventions

* **PascalCase:** Classes, Interfaces, Methods, Properties, Record Types, Namespaces.
* **camelCase:** Local variables, method arguments.
* **`_camelCase`:** Private instance fields (or prefer auto-properties/primary constructors).
* **Interface Prefix:** Always prefix interfaces with `I` (e.g., `IUserRepository`).
* **Async Suffix:** Suffix asynchronous methods with `Async` (e.g., `FetchDataAsync`).

---

## 4. Code Examples

### Good Example: Modern, Asynchronous & Memory-Efficient

```csharp
namespace Application.Services;

public interface IOrderProcessor
{
    Task<Result<OrderResponse>> ProcessOrderAsync(OrderRequest request, CancellationToken cancellationToken = default);
}

// Primary constructor dependency injection
public sealed class OrderProcessor(
    IOrderRepository repository,
    ILogger<OrderProcessor> logger) : IOrderProcessor
{
    public async Task<Result<OrderResponse>> ProcessOrderAsync(
        OrderRequest request, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation("Processing order {OrderId}", request.OrderId);

        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result<OrderResponse>.Failure($"Order {request.OrderId} was not found.");
        }

        // Collection expression and pattern matching usage
        ReadOnlySpan<char> couponCode = request.CouponCode.AsSpan();
        decimal discount = couponCode switch
        {
            "SAVE10" => 0.10m,
            "SAVE20" => 0.20m,
            _ => 0.00m
        };

        var finalPrice = order.TotalAmount * (1 - discount);
        return Result<OrderResponse>.Success(new OrderResponse(order.Id, finalPrice));
    }
}

// Immutable Record DTOs
public record OrderRequest(Guid OrderId, string CouponCode);
public record OrderResponse(Guid OrderId, decimal FinalPrice);