using System;
using System.Collections.Generic;
using System.Text;

namespace Tinble
{
    internal struct Position
    {
        public Position(int line, int column, string source)
        {
            Line = line;
            Column = column;
            Source = source;
        }

        public int Line { get; set; }
        public int Column { get; set; }
        public string Source { get; }
    }
}
