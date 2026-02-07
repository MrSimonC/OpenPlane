using System.Text;
using OpenPlane.Core.Abstractions;

namespace OpenPlane.Core.Services;

public sealed class FileAdapterService : IFileAdapterService
{
    private static readonly HashSet<string> WritableTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".yaml", ".yml", ".xml", ".toml", ".csv", ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".go", ".java", ".rs", ".cpp", ".h", ".hpp", ".csproj", ".sln", ".config", ".sh"
    };

    private static readonly HashSet<string> ExtractOnlyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ipynb"
    };

    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".ipynb", StringComparison.OrdinalIgnoreCase))
        {
            return await ExtractNotebookAsync(path, cancellationToken);
        }

        if (ExtractOnlyExtensions.Contains(extension))
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return $"[extract-only:{extension}] size={bytes.Length} bytes\n" + ExtractPrintableText(bytes, 4000);
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        if (!CanWrite(path))
        {
            throw new InvalidOperationException($"Write-back is unsupported for this file type. {DescribeCapability(path)}");
        }

        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    public bool CanWrite(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return true;
        }

        return WritableTextExtensions.Contains(extension);
    }

    public string DescribeCapability(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension) || WritableTextExtensions.Contains(extension))
        {
            return "Text-native read/write supported.";
        }

        if (ExtractOnlyExtensions.Contains(extension))
        {
            return "Extract-only adapter: read supported, write-back unsupported.";
        }

        return "Unknown type: defaulting to text read/write when possible.";
    }

    private static async Task<string> ExtractNotebookAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("cells", out var cells) || cells.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return "[extract-only:.ipynb] Notebook has no cells array.";
            }

            var lines = new List<string>();
            var index = 1;
            foreach (var cell in cells.EnumerateArray())
            {
                var cellType = cell.TryGetProperty("cell_type", out var typeProp) ? typeProp.GetString() : "unknown";
                lines.Add($"cell#{index} type={cellType}");

                if (cell.TryGetProperty("source", out var source) && source.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var text = string.Concat(source.EnumerateArray().Select(x => x.GetString()));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lines.Add(text.Trim());
                    }
                }

                index++;
            }

            return string.Join(Environment.NewLine, lines);
        }
        catch
        {
            return "[extract-only:.ipynb] Failed to parse notebook JSON.";
        }
    }

    private static string ExtractPrintableText(byte[] bytes, int maxChars)
    {
        if (bytes.Length == 0)
        {
            return "(empty)";
        }

        var text = Encoding.UTF8.GetString(bytes);
        var printable = new string(text.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t').ToArray());
        if (printable.Length <= maxChars)
        {
            return printable;
        }

        return printable[..maxChars] + "\n...[truncated]";
    }
}
