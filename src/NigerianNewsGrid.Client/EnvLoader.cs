namespace NigerianNewsGrid.Client;

/// <summary>
/// Lightweight, zero-dependency .env loader that discovers and loads .env key-value pairs
/// into Environment variables and Configuration providers across all services.
/// </summary>
public static class EnvLoader
{
    private static readonly object _lock = new();

    /// <summary>
    /// Discovers and loads the root .env file into the current process environment.
    /// Safely traverses up parent directories from AppContext.BaseDirectory and Directory.GetCurrentDirectory().
    /// </summary>
    public static Dictionary<string, string> Load(string? startDirectory = null)
    {
        lock (_lock)
        {
            var loadedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var candidatePaths = new List<string>();
            AddCandidateAncestors(candidatePaths, startDirectory ?? Directory.GetCurrentDirectory());
            AddCandidateAncestors(candidatePaths, AppContext.BaseDirectory);

            string? envFilePath = null;
            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    envFilePath = path;
                    break;
                }
            }

            if (envFilePath == null)
            {
                return loadedVars;
            }

            try
            {
                var lines = File.ReadAllLines(envFilePath);
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    var equalsIndex = line.IndexOf('=');
                    if (equalsIndex <= 0)
                    {
                        continue;
                    }

                    var key = line[..equalsIndex].Trim();
                    var value = line[(equalsIndex + 1)..].Trim();

                    // Strip optional quotes
                    if (value.Length >= 2 &&
                        ((value.StartsWith('"') && value.EndsWith('"')) ||
                         (value.StartsWith('\'') && value.EndsWith('\''))))
                    {
                        value = value[1..^1];
                    }

                    loadedVars[key] = value;

                    // Set standard process-level environment variable if not already set by host/container
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }

                    // Convenience aliases for ASP.NET Core configuration mapping
                    if (key.Equals("API_KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ApiKey")))
                            Environment.SetEnvironmentVariable("ApiKey", value);
                    }
                    else if (key.Equals("GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Gemini__ApiKey")))
                            Environment.SetEnvironmentVariable("Gemini__ApiKey", value);
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Gemini:ApiKey")))
                            Environment.SetEnvironmentVariable("Gemini:ApiKey", value);
                    }
                    else if (key.Equals("GEMINI_MODEL", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Gemini__Model")))
                            Environment.SetEnvironmentVariable("Gemini__Model", value);
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Gemini:Model")))
                            Environment.SetEnvironmentVariable("Gemini:Model", value);
                    }
                    else if (key.Equals("INDEXNOW_KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IndexNow__Key")))
                            Environment.SetEnvironmentVariable("IndexNow__Key", value);
                    }
                }
            }
            catch
            {
                // Soft fail: ignore filesystem permissions or formatting errors during startup
            }

            return loadedVars;
        }
    }

    private static void AddCandidateAncestors(List<string> candidates, string startDir)
    {
        try
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var envFile = Path.Combine(dir.FullName, ".env");
                if (!candidates.Contains(envFile))
                {
                    candidates.Add(envFile);
                }
                dir = dir.Parent;
            }
        }
        catch { }
    }
}
