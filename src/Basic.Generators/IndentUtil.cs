using System;
using System.Collections.Generic;
using System.Text;

namespace Basic.Generators;

internal sealed class IndentUtil
{
    private sealed class Marker : IDisposable
    {
        private readonly IndentUtil _util;
        private int _count;

        public Marker(IndentUtil indentUtil, int count)
        {
            _util = indentUtil;
            _count = count;
        }

        public void Revert()
        {
            Dispose();
            _count = 0;
        }

        public void Dispose() => _util.Decrease(_count);
    }

    public int Depth { get; private set; }
    public string UnitValue { get; } = new string(' ', 4);
    public string Value { get; private set; } = "";

    public string GetValue(int depth)
    {
        if (depth == 0)
        {
            return Value;
        }

        return new string(' ', (Depth + depth) * 4);
    }

    public IndentUtil() => Update();

    public IDisposable Increase(int depth = 1)
    {
        IncreaseSimple(depth);
        return new Marker(this, depth);
    }

    public void IncreaseSimple(int depth = 1)
    {
        Depth += depth;
        Update();
    }

    public void Decrease(int count = 1)
    {
        Depth -= count;
        Update();
    }

    private void Update()
    {
        Value = new string(' ', Depth * 4);
    }
}
