namespace Api.Utils;

public static class NameUtils
{
    public const int MaxLength = 24;

    public static string? Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var trimmed = name.Trim();
        return trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
    }
}
