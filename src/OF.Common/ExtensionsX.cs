using Ardalis.GuardClauses;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace OF.Common;

public static class ExtensionsX
{
    public static bool TryGetValue<T>(this IReadOnlyDictionary<string, object> cache, string key, out T? value)
    {
        Guard.Against.Null(cache, nameof(cache));
        Guard.Against.Null(key, nameof(key));
        value = default;
        return cache.TryGetValue(key, out var val) && val.IsConvertible(out value);
    }

    private static bool IsConvertible<T>(this object? val, out T? value)
    {
        var isConvertible =
            val is T? ||
            val is null &&
                (!typeof(T).IsValueType ||
                Nullable.GetUnderlyingType(typeof(T)) is not null);
        value = isConvertible ? (T?)val : default;
        return isConvertible;
    }

    /// <summary>
    ///     Reads, optionally truncates with an optional suffix, string content from the response.
    /// </summary>
    private static async Task<string> ReadStringContent(
        this HttpContent content,
        int max = int.MaxValue,
        string suffix = "...",
        CancellationToken cancellationToken = default)
    {
        await content.LoadIntoBufferAsync();
        return (await content.ReadAsStringAsync(cancellationToken)).AtMost(max, suffix);
    }

    /// <summary>
    ///     Reads, optionally truncates with an optional suffix, string content from the response.
    /// </summary>
    public static async Task<string?> ReadStringContent(
        this HttpResponseMessage message,
        int max = int.MaxValue,
        string suffix = "...",
        CancellationToken cancellationToken = default)
    {
        if (message?.Content is not null
            && (message.Content.Headers.ContentTypeIsText() == true
            || message.Headers.ContentTypeIsText() == true))
        {
            return await message.Content.ReadStringContent(max, suffix, cancellationToken);
        }

        return null;
    }

    /// <summary>
    ///     Reads, optionally truncates with an optional suffix, string content from the request.
    /// </summary>
    public static async Task<string> ReadStringContent(
        this HttpRequestMessage message,
        int max = int.MaxValue,
        string suffix = "...",
        CancellationToken cancellationToken = default)
    {
        if (message?.Content is not null
            && (message.Content.Headers.ContentTypeIsText() == true
            || message.Headers.ContentTypeIsText() == true))
        {
            return await message.Content.ReadStringContent(max, suffix, cancellationToken);
        }

        return null;
    }

    private static IReadOnlyCollection<string> TextContentTypes { get; }
        = new[]
        {
            "html",
            "text",
            "xml",
            "json",
            "txt",
            "x-www-form-urlencoded"
        };

    public static bool ContentTypeIsText(this HttpHeaders headers)
    {
        if (headers?.TryGetValues("Content-Type", out var values) == true)
        {
            var comparer = CultureInfo.InvariantCulture.CompareInfo;
            return values
                .SelectMany(_ => TextContentTypes, (v, x) => comparer.IndexOf(v, x, CompareOptions.OrdinalIgnoreCase) >= 0)
                .Any(found => found);
        }

        return false;
    }

    /// <summary>
    /// Truncates the string if its length is more than <paramref name="maxLength"/> and appends a <paramref name="suffix"/> if any.
    /// The total number of characters of the resulting string will always be <paramref name="maxLength"/> at most.
    /// </summary>
    [return: NotNullIfNotNull(nameof(str))]
    public static string? AtMost(this string? str, int maxLength, string? suffix)
    {
        if (suffix?.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(suffix), suffix?.Length ?? 0, $"Should be lesser than or equal to {suffix?.Length ?? 0}");
        }

        if (str?.Length > maxLength)
        {
            if (suffix?.Length > 0)
            {
                str = string.Create(
                    maxLength,
                    (str, suffix, maxLength),
                    (dest, arg) =>
                    {
                        arg.str.AsSpan()[..(arg.maxLength - arg.suffix.Length)].CopyTo(dest);
                        arg.suffix.AsSpan().CopyTo(dest[^arg.suffix.Length..]);
                    });
            }
            else
            {
                str = str[..maxLength];
            }
        }

        return str;
    }

    /// <summary>
    /// Gets the message from the exception and any inner exceptions
    /// </summary>
    public static string FullMessage(this Exception ex)
    {
        var message = new StringBuilder(ex.Message);

        while (ex.InnerException != null)
        {
            ex = ex.InnerException;

            message.Append(" -> ");
            message.Append(ex.Message);
        }

        return message.ToString();
    }

    /// <summary>
    /// Attempts to read the first value from the specified header.
    /// </summary>
    public static bool TryReadFirst<T>(this HttpHeaders headers, string header, out T value)
    {
        if (headers.TryGetValues(header, out IEnumerable<string>? values) && values.Any())
        {
            try
            {
                value = (T)Convert.ChangeType(values.First(), typeof(T));
                return true;
            }
            catch
            {
                // Conversion has failed.
            }
        }

        value = default;
        return false;
    }
}
