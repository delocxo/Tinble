using System;
using System.Collections.Generic;
using System.Text;

namespace Tinble
{
    internal class Error : Exception
    {
        public static Error NoPosition(string message) => new Error(message, null);

        Position? _position;

        public Error(string message, Position? position) : base(message)
        {
            _position = position;
        }

        public void Print()
        {
            if (_position != null)
                Console.Error.WriteLine($"Compiler Error: {_position?.Source}:{_position?.Line}:{_position?.Column}: {Message}");
            else
                Console.Error.WriteLine($"Compiler Error: {Message}");
        }
    }
}
