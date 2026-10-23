namespace SysScope.Core.Models;

public sealed record ConnectionTarget(string Computer, string? Username, string? Password)
{
    public string ComputerName
    {
        get
        {
            var value = (Computer ?? "").Trim().TrimStart('\\');
            return value;
        }
    }

    public bool IsLocal
    {
        get
        {
            var name = ComputerName;
            return name.Length == 0
                || name.Equals(".", StringComparison.Ordinal)
                || name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || name.Equals("local", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(Username);
}

public sealed record CollectRequest(ConnectionTarget Target, string DisplayName);
