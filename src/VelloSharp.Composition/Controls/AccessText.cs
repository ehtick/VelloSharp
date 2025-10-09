using System.Text;

namespace VelloSharp.Composition.Controls;

public class AccessText : TextBlock
{
    protected override string? GetTextForRendering()
    {
        var raw = Text;
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        var builder = new StringBuilder(raw.Length);
        var span = raw.AsSpan();
        int index = 0;
        while (index < span.Length)
        {
            var ch = span[index];
            if (ch == '_')
            {
                if (index + 1 < span.Length)
                {
                    var next = span[index + 1];
                    if (next == '_')
                    {
                        builder.Append('_');
                        index += 2;
                        continue;
                    }

                    builder.Append(next);
                    index += 2;
                    continue;
                }

                index++;
                continue;
            }

            builder.Append(ch);
            index++;
        }

        return builder.ToString();
    }
}
