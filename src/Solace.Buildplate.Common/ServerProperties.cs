using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using BitcoderCZ.IO;

namespace Solace.Buildplate.Common;

#pragma warning disable CA1710 // Identifiers should have correct suffix
public sealed class ServerProperties : IDictionary<string, string>
#pragma warning restore CA1710 // Identifiers should have correct suffix
{
    private readonly Dictionary<string, string> _dict;

    public ServerProperties()
    {
        _dict = [];
    }

    private ServerProperties(Dictionary<string, string> dict)
    {
        _dict = dict;
    }

    public static async Task<ServerProperties> LoadAsync(IFile path, CancellationToken cancellationToken = default)
    {
        var dict = new Dictionary<string, string>();
        var lineBuffer = new StringBuilder();

        await foreach (var rawLine in path.ReadLinesAsync(cancellationToken))
        {
            var currentLine = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            if (lineBuffer.Length > 0)
            {
                currentLine = currentLine.TrimStart();
                lineBuffer.Append(currentLine);
            }
            else
            {
                var trimmed = currentLine.TrimStart();
                if (trimmed.Length is 0 || trimmed.StartsWith('#') || trimmed.StartsWith('!'))
                {
                    continue;
                }

                lineBuffer.Append(currentLine);
            }

            if (IsLineContinued(lineBuffer))
            {
                lineBuffer.Length--;
                continue;
            }

            ParsePropertyLine(lineBuffer.ToString(), dict);
            lineBuffer.Clear();
        }

        if (lineBuffer.Length > 0)
        {
            ParsePropertyLine(lineBuffer.ToString(), dict);
        }

        return new ServerProperties(dict);
    }

    public async Task SaveAsync(IFile path, CancellationToken cancellationToken = default)
        => await path.WriteAllLinesAsync(_dict.Select(item => $"{item.Key}={item.Value}"), cancellationToken);

    private static bool IsLineContinued(StringBuilder builder)
    {
        var backslashCount = 0;
        for (var i = builder.Length - 1; i >= 0 && builder[i] is '\\'; i--)
        {
            backslashCount++;
        }

        return backslashCount % 2 != 0;
    }

    private static void ParsePropertyLine(ReadOnlySpan<char> line, Dictionary<string, string> dict)
    {
        line = line.TrimStart();
        if (line.IsEmpty || line.StartsWith('#') || line.StartsWith('!'))
        {
            return;
        }

        var keyEnd = -1;
        var valueStart = -1;
        var inEscape = false;

        for (var i = 0; i < line.Length; i++)
        {
            if (inEscape)
            {
                inEscape = false;
                continue;
            }

            var currentChar = line[i];
            if (currentChar == '\\')
            {
                inEscape = true;
                continue;
            }

            if (currentChar is '=' or ':' || char.IsWhiteSpace(currentChar))
            {
                keyEnd = i;
                var separatorIndex = i;

                while (separatorIndex < line.Length && char.IsWhiteSpace(line[separatorIndex]))
                {
                    separatorIndex++;
                }

                if (separatorIndex < line.Length && (line[separatorIndex] is '=' or ':'))
                {
                    separatorIndex++;
                    while (separatorIndex < line.Length && char.IsWhiteSpace(line[separatorIndex]))
                    {
                        separatorIndex++;
                    }
                }

                valueStart = separatorIndex;
                break;
            }
        }

        string key;
        string value;

        if (keyEnd is -1)
        {
            key = Unescape(line);
            value = string.Empty;
        }
        else
        {
            key = Unescape(line[..keyEnd]);
            value = Unescape(line[valueStart..]);
        }

        dict[key] = value;
    }

    private static string Unescape(ReadOnlySpan<char> input)
    {
        if (!input.Contains('\\'))
        {
            return input.ToString();
        }

        var builder = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] is '\\' && i + 1 < input.Length)
            {
                i++;

                var currentChar = input[i];
                switch (currentChar)
                {
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'u' when i + 4 < input.Length && int.TryParse(input.Slice(i + 1, 4), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var hex):
                        builder.Append((char)hex);
                        i += 4;
                        break;
                    default:
                        builder.Append(currentChar);
                        break;
                }
            }
            else
            {
                builder.Append(input[i]);
            }
        }

        return builder.ToString();
    }

    public string this[string key]
    {
        get => _dict[key];
        set => _dict[key] = value;
    }

    public ICollection<string> Keys => _dict.Keys;

    public ICollection<string> Values => _dict.Values;

    public int Count => _dict.Count;

    public bool IsReadOnly => false;

    public void Add(string key, string value)
        => _dict.Add(key, value);

    void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
        => Add(item.Key, item.Value);

    public void Clear()
        => _dict.Clear();

    public bool ContainsKey(string key)
        => _dict.ContainsKey(key);

    bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
        => ((ICollection<KeyValuePair<string, string>>)_dict).Contains(item);

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        => ((ICollection<KeyValuePair<string, string>>)_dict).CopyTo(array, arrayIndex);

    public bool Remove(string key)
        => _dict.Remove(key);

    bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
        => ((ICollection<KeyValuePair<string, string>>)_dict).Remove(item);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
        => _dict.TryGetValue(key, out value);

    public Dictionary<string, string>.Enumerator GetEnumerator()
        => _dict.GetEnumerator();

    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
