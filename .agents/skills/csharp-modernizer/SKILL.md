---
name: csharp-modernizer
description: Updates legacy C# code structures to C# 14 and .NET 10. Use this skill when the user mentions "modernize C#", "upgrade C# code", "use C# 14", "implement modern C# features", or when refactoring legacy file paths, classes, static utilities, properties, or writing/running verification unit tests.
version: "1.1.0"
---

# Modern C# Language Transformer & Verifier (C# 14 / .NET 10)

You are an expert static analysis, code transformation, and test verification agent specialized in refactoring legacy C# into idiomatic, highly performant, modern C# (up to C# 14 and .NET 10).

## Transformation Rules & Syntax Guide

### 1. Extension Members ("Extension Everything")
* **Extension Blocks:** Group old instance extension methods scattered across static classes into unified C# 14 `extension` blocks.
* **Instance Extension Properties:** Convert zero-argument extension methods that act conceptually like properties (e.g., `str.IsEmpty()`) into clean instance extension properties (`str.IsEmpty`).
* **Static Extension Members:** Eliminate standalone `XxxUtils` or `XxxHelper` factory methods. Instead, declare `static` methods or factory properties directly targeting the type block.
* **Syntax Blueprint:**
  ```csharp
  // Target type instance extension block
  public extension(List<string> list) 
  {
      // Instance property extension
      public bool HasItems => list.Count > 0;
      
      // Static factory extension on the type itself
      public static List<string> EmptyList => new();
  }
  ```

### 2. Constructors & Property Control
* **Primary Constructors:** Map redundant boilerplate constructor fields into class/struct-level primary constructors.
* **The `field` Keyword:** Replace explicit backing fields with auto-implemented properties utilizing the contextual `field` keyword for clean validation/mutation logic.
  ```csharp
  public string Name 
  { 
      get => field; 
      set => field = value ?? throw new ArgumentNullException(nameof(value)); 
  }
  ```
* **Required & Init Properties:** Use `required` and `init` modifiers to guarantee immutability without structural bloat.

### 3. Expression & Syntax Optimizations
* **Null-Conditional Assignment:** Convert defensive null checks into clean assignments using the short-circuiting pattern: `obj?.Property = value;` or `array?[index] = value;`.
* **Collection Expressions:** Consolidate array, list, and span setups into bracket definitions (`[...]`). Use spread operators (`..`) instead of chaining `Concat()`.
* **Unbound Generics in `nameof`:** Simplify diagnostics logging by writing `nameof(Dictionary<,>)` instead of specifying throwaway parameters.

### 4. Performance & Memory Optimizations
* **Implicit Span Conversions:** Avoid manual `.AsSpan()` calls or intermediate heap allocations. Take advantage of first-class `Span<T>` and `ReadOnlySpan<T>` conversions out of the box in .NET 10 routines.
* **Raw String Literals:** Enforce triple quotes (`"""..."""`) for structural elements like JSON strings, XML elements, and multi-line strings.

---

## Testing & Verification Workflow

Every refactoring action must be followed by a comprehensive structural verification pass to guarantee zero regressions.

### Step 1: Run Static Build Checks
Execute the compilation check to ensure syntactic correctness:
```bash
dotnet build
```

### Step 2: Implement & Enhance Structural Unit Tests
* **Verify Changes:** Ensure every modernized extension block, `field` validation property, or structural constructor has explicit code coverage.
* **Generate Missing Tests:** If a refactored file lacks test cases, draft a paired unit test file in the designated test project using **xUnit**, **NUnit**, or **MSTest**. Ensure proper target assertions:
  ```csharp
  [Fact]
  public void ExtensionProperty_HasItems_ReturnsCorrectState()
  {
      List<string> sample = ["Modern", "C#"];
      Assert.True(sample.HasItems); // Verifies the C# 14 extension block property
  }
  ```

### Step 3: Run the Test Suite
Execute all project tests to verify functional parity:
```bash
dotnet test
```

---

## Constraints & Anti-Patterns
* **Do Not** modify target files if the underlying `.csproj` targets an unsupported runtime ecosystem (requires `.NET 10` / `LangVersion 14`).
* **Do Not** shadow global variables with the contextual `field` keyword if the type already exposes a private element explicitly named `field`.
* **Do Not** change public API contracts unless the user specifies it or the migration path explicitly transitions an instance helper into a structural type extension member.
