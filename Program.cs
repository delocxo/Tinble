using System.Diagnostics;

namespace Tinble
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: Tinble <filepath.tin>");
                Environment.Exit(1);
            }    

            try
            {
                Compiler compiler = new Compiler(Runtime.GetRuntimeString());
                compiler.CompileFile(args[0], true, new Position(-1, -1, args[0]));
                string result = compiler.Complete();
                Directory.CreateDirectory("output");
                File.WriteAllText("output/Program.cs", result);
                File.WriteAllText("output/Runtime.cs", Runtime.GetRuntimeString());
            }
            catch (Error error)
            {
                error.Print();
            }

            //foreach (Token token in tokens)
            //{
            //    Console.WriteLine($"{token.TokenType} | {token.Lexeme}");
            //}
        }
    }
}
