using System;
using System.Collections.Generic;
using System.Text;

namespace Basic.Generators;

internal sealed class CodeBuilder(StringBuilder? builder = null)
{
    internal StringBuilder Builder { get; } = builder ?? new StringBuilder();

    public void AppendIndent(int spaces)
    {
        for (int i = 0; i < spaces; i++)
        {
            Builder.Append(' ');
        }
    }

    public void Append(string value) => Builder.Append(value);

    public void Append(int indent, string value)
    {
        AppendIndent(indent);
        Builder.Append(value);
    }

    public void AppendLine(string line) => Builder.AppendLine(line);

    public void AppendLine(int indent, string line)
    {
        AppendIndent(indent);
        Builder.AppendLine(line);
    }

    public override string ToString() => Builder.ToString();
}
