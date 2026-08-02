using System;
using System.Collections.Generic;
using System.Text;

namespace Tinble
{
    internal class Runtime
    {
        public static string GetRuntimeString()
        {
            string exePath = AppContext.BaseDirectory;
            string runtimePath = Path.Join(exePath, "RuntimeTemplate.cs");
            if (!File.Exists(runtimePath))
                throw new Exception("Failed To Find Runtime Path");
            return File.ReadAllText(runtimePath);
        }

        public static string PosToString(Position position) => $"new Position({position.Line}, {position.Column}, \"{position.Source}\")";
        public static string NewValue(string value) => $"new Value({value})";
    }
}
