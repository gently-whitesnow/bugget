namespace Authorization.Abstractions;

public static class SanitizeHelper
{
    public static string? SanitizeLocalPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(raw);
        }
        catch
        {
            return null;
        }

        if (!decoded.StartsWith('/'))
        {
            return null; // must be absolute local path
        }

        if (decoded.StartsWith("//"))
        {
            return null; // no protocol-relative
        }

        if (decoded.Contains("://", StringComparison.Ordinal))
        {
            return null; // no absolute URLs
        }

        if (decoded.Contains('\n') || decoded.Contains('\r'))
        {
            return null; // no newlines
        }

        // Optionally, collapse duplicated slashes except the leading one
        var span = decoded.AsSpan();
        var result = new System.Text.StringBuilder(decoded.Length);
        result.Append('/');
        var i = 1;
        while (i < span.Length)
        {
            var c = span[i];
            if (c == '/')
            {
                // skip consecutive slashes
                while (i < span.Length && span[i] == '/')
                {
                    i++;
                }

                result.Append('/');
                continue;
            }
            result.Append(c);
            i++;
        }
        return result.ToString();
    }
}
