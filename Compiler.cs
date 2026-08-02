using System;
using System.Collections.Generic;
using System.Text;

namespace Tinble
{
    internal class FunctionInfo
    {
        public FunctionInfo(string name, int arity)
        {
            Name = name;
            Arity = arity;
        }

        public string Name { get; }
        public int Arity { get; }
    }

    internal class Compiler
    {
        public StringBuilder StringBuilder { get; } = new StringBuilder();
        public Stack<Dictionary<string, string>> Scopes { get; } = new Stack<Dictionary<string, string>>();
        public Dictionary<string, int> NextLocalIndexs { get; } = new Dictionary<string, int>();
        public Dictionary<string, Dictionary<int, FunctionInfo>> Functions { get; } = new Dictionary<string, Dictionary<int, FunctionInfo>>();

        int _indentSize = 4;
        int _indexLevel = 2;
        bool _inLoop = false;
        bool _inFunction = false;
        int _nextForId = 0;
        int _nextAnonymousId = 0;
        Dictionary<string, string> _structs = [];
        Dictionary<string, bool> _compileFiles = [];
        Dictionary<string, string> _enums = [];
        
        public Compiler(string runtimeText)
        {
            Scopes.Push(new Dictionary<string, string>());
        }

        public string Complete()
        {
            string target =
                $$"""
                using System;
                using System.Collections.Frozen;
                using System.Collections.Generic;
                using System.Text;

                namespace TinbleGenerated;
                
                // User
                class Program
                {
                    static void Main(string[] arguments)
                    {
                {{StringBuilder.ToString()}}
                    }
                }
                """;

            return target;
        }

        public void CompileFile(string source, bool isEntry, Position position)
        {
            if (!File.Exists(source))
                throw new Error($"File '{source}' does not exist", position);

            if (_compileFiles.TryGetValue(source, out bool finished))
            {
                if (!finished)
                    throw new Error($"Circular import detected: '{source}'", position);
                return;
            }

            _compileFiles[source] = false;

            Lexer lexer = new Lexer(source);
            Parser parser = new Parser(lexer.Tokenize());
            List<Stmt> ast = parser.Parse();

            foreach (Stmt stmt in ast)
                if (stmt is FuncStmt funcStmt)
                {
                    FunctionInfo function = new FunctionInfo($"_{funcStmt.Name}_{funcStmt.Args.Count}", funcStmt.Args.Count);
                    if (Functions.TryGetValue(funcStmt.Name, out var overloads))
                    {
                        if (overloads.ContainsKey(funcStmt.Args.Count))
                            throw new Error($"'{funcStmt.Name}' with {funcStmt.Args.Count} argument(s) already exist", funcStmt.Position);
                        overloads.Add(funcStmt.Args.Count, function);
                        continue;
                    }
                    Functions.Add(funcStmt.Name, new Dictionary<int, FunctionInfo>() { {funcStmt.Args.Count, function} });
                }
                else if (stmt is ImportStmt importStmt)
                    CompileFile(importStmt.FilePath, false, importStmt.Position);

            foreach (Stmt stmt in ast)
            {
                if (!isEntry && stmt is not (FuncStmt or ImportStmt or StructStmt or EnumStmt))
                    throw new Error($"Top-level executable statements are not allowed in imported file '{source}'", stmt.Position);
                CompileStmt(stmt);
            }

            _compileFiles[source] = true;
        }

        void CompileStmt(Stmt stmt)
        {
            switch (stmt)
            {
                case FuncStmt funcStmt:
                    {
                        if (Scopes.Count > 1)
                            throw new Error("Function cannot be declared at the local scope", funcStmt.Position);

                        VerfiyStringArguments(funcStmt.Args, (member) => $"'{member}' is a duplicate function arguments", funcStmt.Position);

                        string csharpName = Functions[funcStmt.Name][funcStmt.Args.Count].Name;

                        bool wasInLoop = _inLoop;

                        _inLoop = false;
                        _inFunction = true;

                        EmitLine($"Value {csharpName}(Value[] args, Position position)");
                        EmitLine("{");
                        BeginScope();

                        for (int i = 0; i < funcStmt.Args.Count; i++)
                        {
                            string arg = funcStmt.Args[i];

                            string argCsharpName = SetLocal(arg, funcStmt.Position);

                            EmitLine($"Value {argCsharpName} = args[{i}];");
                        }

                        foreach (Stmt bodyStmt in funcStmt.Body)
                            CompileStmt(bodyStmt);

                        if (funcStmt.Body.Count == 0 || funcStmt.Body.Last() is not ReturnStmt)
                            EmitLine("return Value.Null();");

                        if (funcStmt.Body.Last() is ReturnStmt returnStmt && returnStmt.Condition != null)
                            EmitLine("return Value.Null();");

                        EndScope();
                        EmitLine("}");

                        _inLoop = wasInLoop;
                        _inFunction = false;
                        break;
                    }

                case ImportStmt importStmt:
                    if (Scopes.Count > 1)
                        throw new Error($"Cannot import in a local scope", importStmt.Position);
                    break;

                case VarStmt varStmt:
                    {
                        string csName = SetLocal(varStmt.Name, varStmt.Position);
                        string expr = CompileExpr(varStmt.Expr);
                        EmitLine($"Value {csName} = {expr};");
                        break;
                    }

                case VarReassignStmt varReassignStmt:
                    {
                        string csName = ResolveLocal(varReassignStmt.Name, varReassignStmt.Position);
                        string expr = CompileExpr(varReassignStmt.Expr);
                        EmitLine($"{csName} = {expr};");
                        break;
                    }

                case IfStmt ifStmt:
                    {
                        CompileCondition("if", ifStmt.If);
                        foreach (ConditionStmt elseIf in ifStmt.ElseIfs)
                            CompileCondition("else if", elseIf);
                        if (ifStmt.ElseBody != null)
                        {
                            EmitLine("else");
                            EmitLine("{");
                            CompileBody(ifStmt.ElseBody);
                            EmitLine("}");
                        }
                        break;
                    }

                case WhileStmt whileStmt:
                    {
                        CompileCondition("while", whileStmt.While, true);
                        break;
                    }

                case BreakStmt breakStmt:
                    {
                        if (!_inLoop)
                            throw new Error("Cannot use break outside a loop", breakStmt.Position);

                        if (breakStmt.Condition != null)
                        {
                            string condition = CompileExpr(breakStmt.Condition);

                            EmitLine($"if ({RuntimeOpToString("IsTruthy", condition)}) break;");
                            break;
                        }

                        EmitLine("break;");
                        break;
                    }

                case ContinueStmt continueStmt:
                    {
                        if (!_inLoop)
                            throw new Error("Cannot use continue outside a loop", continueStmt.Position);

                        if (continueStmt.Condition != null)
                        {
                            string condition = CompileExpr(continueStmt.Condition);

                            EmitLine($"if ({RuntimeOpToString("IsTruthy", condition)}) continue;");
                            break;
                        }

                        EmitLine("continue;");
                        break;
                    }

                case ReturnStmt returnStmt:
                    {
                        if (!_inFunction)
                            throw new Error("Can only return in a function", returnStmt.Position);

                        string expr = CompileExpr(returnStmt.Value);

                        if (returnStmt.Condition == null)
                        {
                            EmitLine($"return {expr};");
                            break;
                        }

                        string condition = CompileExpr(returnStmt.Condition);

                        EmitLine($"if ({RuntimeOpToString("IsTruthy", condition)}) return {expr};");

                        break;
                    }

                case CallStmt callStmt:
                    {
                        string expr = $"{CompileExpr(callStmt.CallExpr)}";

                        if (callStmt.Condition != null)
                        {
                            string condition = CompileExpr(callStmt.Condition);
                            EmitLine($"if ({RuntimeOpToString("IsTruthy", condition)}) {expr};");
                            if (callStmt.ElseCall != null)
                                EmitLine($"else {CompileExpr(callStmt.ElseCall)};");
                            break;
                        }

                        EmitLine($"{expr};");
                        break;
                    }

                case IndexSetStmt indexSetExpr:
                    {
                        IndexGetExpr indexGetExpr = indexSetExpr.IndexGetExpr;
                        string target = CompileExpr(indexGetExpr.Target);
                        string index = CompileExpr(indexGetExpr.Index);
                        string value = CompileExpr(indexSetExpr.Value);
                        EmitLine($"{RuntimeOpToString("IndexSet", $"{target}, {index}, {value}", indexGetExpr.Position)};");
                        break;
                    }

                case ForStmt forStmt:
                    {
                        int id = _nextForId++;

                        string iterableName = $"__iterable_{id}";
                        string counterName = $"__index_{id}";
                        string itemName = $"__item_{id}";
                        string iterable = CompileExpr(forStmt.Target);

                        EmitLine($"List<Value> {iterableName} = {RuntimeOpToString("GetIterable", iterable, forStmt.Position)};");

                        if (forStmt.IndexName != null)
                            EmitLine($"long {counterName} = 0;");

                        EmitLine($"foreach (Value {itemName} in {iterableName})");
                        EmitLine("{");
                        BeginScope();

                        string valueName = SetLocal(forStmt.ValueName, forStmt.Position);

                        EmitLine($"Value {valueName} = {itemName};");

                        if (forStmt.IndexName != null)
                        {
                            string indexName = SetLocal(forStmt.IndexName, forStmt.Position);
                            EmitLine($"Value {indexName} = new Value({counterName}++);");
                        }

                        bool wasInLoop = _inLoop;
                        _inLoop = true;

                        foreach (Stmt bodyStmt in forStmt.Body)
                            CompileStmt(bodyStmt);

                        _inLoop = wasInLoop;

                        EndScope();
                        EmitLine("}");
                        break;
                    }

                case StructStmt structStmt:
                    {
                        if (Scopes.Count > 1)
                            throw new Error("Struct cannot be declared at the local scope", structStmt.Position);

                        if (_structs.ContainsKey(structStmt.Name))
                            throw new Error($"Struct '{structStmt.Name}' already exist", structStmt.Position);

                        HashSet<string> members = [];

                        foreach (string member in structStmt.Members)
                            if (!members.Add(member))
                                throw new Error($"'{member}' is a duplicate member inside struct '{structStmt.Name}'", structStmt.Position);

                        List<string> pairs = members.Select((member, i) => $"{{ \"{member}\", {i} }}").ToList();

                        string structDict = $"new Dictionary<string, int>() {{ {string.Join(", ", pairs)} }}";

                        EmitLine($"{RuntimeOpToString("RegisterStruct", $"new StructType(\"{structStmt.Name}\", {structDict})", structStmt.Position)};");
                        break;
                    }

                case MemberSetStmt memberSetStmt:
                    {
                        MemberGetExpr memberGetExpr = memberSetStmt.MemberGetExpr;
                        string target = CompileExpr(memberGetExpr.Target);
                        string value = CompileExpr(memberSetStmt.Value);
                        EmitLine($"{RuntimeOpToString("MemberSet", $"{target}, {value}, \"{memberGetExpr.Name}\"", memberGetExpr.Position)};");
                        break;
                    }

                case EnumStmt enumStmt:
                    {
                        if (Scopes.Count > 1)
                            throw new Error("Enum cannot be declared at the local scope", enumStmt.Position);

                        if (_structs.ContainsKey(enumStmt.Name))
                            throw new Error($"Enum '{enumStmt.Name}' already exist", enumStmt.Position);

                        VerfiyStringArguments(enumStmt.Enums, (member) => $"'{member}' is a duplicate enum value", enumStmt.Position);

                        List<string> enumValues = enumStmt.Enums.Select((member) => $"\"{member}\"").ToList();

                        string structDict = $"new List<string>() {{ {string.Join(", ", enumValues)} }}";

                        EmitLine($"{RuntimeOpToString("RegisterEnum", $"new EnumType(\"{enumStmt.Name}\", {structDict})", enumStmt.Position)};");
                        break;
                    }
            }
        }

        void CompileCondition(string controlFlowName, ConditionStmt conditionStmt, bool isLoop = false)
        {
            string condition = CompileExpr(conditionStmt.Condition);

            EmitLine($"{controlFlowName} ({RuntimeOpToString("IsTruthy", condition)})");
            EmitLine("{");

            bool wasInLoop = _inLoop;

            if (isLoop)
                _inLoop = true;

            CompileBody(conditionStmt.Body);

            if (isLoop)
                _inLoop = wasInLoop;

            EmitLine("}");
        }

        void CompileBody(List<Stmt> stmts)
        {
            BeginScope();
            foreach (Stmt stmt in stmts)
                CompileStmt(stmt);
            EndScope();
        }

        string CompileExpr(Expr expr)
        {
            switch (expr)
            {
                case IntExpr intExpr:
                        return Runtime.NewValue($"{intExpr.Value}L");

                case FloatExpr floatExpr:
                        return Runtime.NewValue($"{floatExpr.Value}D");

                case StringExpr stringExpr:
                        return Runtime.NewValue($"\"{ToCSharpString(stringExpr.Value)}\"");

                case BoolExpr boolExpr:
                    return Runtime.NewValue($"{(boolExpr.Value ? "true" : "false")}");

                case NullExpr nullExpr:
                    return "Value.Null()";

                case NameExpr nameExpr:
                    {
                        if (Functions.TryGetValue(nameExpr.Name, out var overloads))
                        {
                            if (overloads.Count == 1)
                            {
                                FunctionInfo functionInfo = overloads.First().Value;
                                return $"new Value(new Function({functionInfo.Arity}, {functionInfo.Name}, \"{nameExpr.Name}\", ArgMode.Expected))";
                            }
                            throw new Error($"Cannot use overloaded function '{nameExpr.Name}' as a value", nameExpr.Position);
                        }

                        if (TryResolveLocal(nameExpr.Name, out string? local))
                            return $"{local}";

                        return RuntimeOpToString("GetName", $"\"{nameExpr.Name}\"", nameExpr.Position);
                    }

                case UnaryExpr unaryExpr:
                    {
                        string right = CompileExpr(unaryExpr.Right);
                        if (unaryExpr.Op == TokenType.Sub)
                            return RuntimeOpToString("Negate", right, unaryExpr.Position);
                        else
                            return RuntimeOpToString("Flip", right, unaryExpr.Position);
                    }

                case CallExpr callExpr:
                    {
                        if (callExpr.Callee is NameExpr nameExpr && Functions.ContainsKey(nameExpr.Name))
                        {
                            FunctionInfo functionInfo = ResolveOverload(nameExpr.Name, callExpr.Arguments.Count, callExpr.Position);
                            return FunctionInfoToString(functionInfo, nameExpr.Name);
                        }
                        string callee = CompileExpr(callExpr.Callee);
                        string args = string.Join(", ", callExpr.Arguments.Select(CompileExpr));
                        return RuntimeOpToString("Call", $"{callee}, [{args}]", callExpr.Position);
                    }

                case ArrayExpr arrayExpr:
                    {
                        string elements = string.Join(", ", arrayExpr.Elements.Select(CompileExpr));
                        return Runtime.NewValue($"[{elements}]");
                    }

                case IndexGetExpr indexGetExpr:
                    {
                        string target = CompileExpr(indexGetExpr.Target);
                        string index = CompileExpr(indexGetExpr.Index);
                        return RuntimeOpToString("IndexGet", $"{target}, {index}", indexGetExpr.Position);
                    }

                case MemberGetExpr memberGetExpr:
                    {
                        string target = CompileExpr(memberGetExpr.Target);
                        return RuntimeOpToString("MemberGet", $"{target}, \"{memberGetExpr.Name}\"", memberGetExpr.Position);
                    }

                case DictExpr dictExpr:
                    {
                        List<string> pairs = dictExpr.KeyValuePairs.Select(x => $"new KeyValuePair<Value, Value>({CompileExpr(x.Key)}, {CompileExpr(x.Value)})").ToList();
                        string listOfKeyValues = $"new List<KeyValuePair<Value, Value>>() {{ { string.Join(", ", pairs) } }}";
                        return Runtime.NewValue($"new Dict({listOfKeyValues}, {Runtime.PosToString(dictExpr.Position)})"); 
                    }

                case FuncExpr funcExpr:
                    {
                   
                       VerfiyStringArguments(funcExpr.Args, (arg) => $"'{arg}' is duplicate anonymous function arg", funcExpr.Position);

                       int id = _nextAnonymousId++;
                       string name = $"Anonymous{id}";
                       string csharpName = $"__anonymous_{id}";
                       bool wasInLoop = _inLoop;

                       _inLoop = false;
                       _inFunction = true;
                       EmitLine($"FunctionDelegate {csharpName} = (args, pos) => ");
                       EmitLine("{");
                       BeginScope();

                       for (int i = 0; i < funcExpr.Args.Count; i++)
                       {
                           string arg = funcExpr.Args[i];
                           string argCsharpName = SetLocal(arg, funcExpr.Position);
                           EmitLine($"Value {argCsharpName} = args[{i}];");
                       }

                       foreach (Stmt bodyStmt in funcExpr.Body)
                           CompileStmt(bodyStmt);

                       if (funcExpr.Body.Count == 0 || funcExpr.Body.Last() is not ReturnStmt)
                           EmitLine("return Value.Null();");

                       if (funcExpr.Body.Last() is ReturnStmt returnStmt && returnStmt.Condition != null)
                           EmitLine("return Value.Null();");

                       EndScope();
                       EmitLine("};");

                       _inLoop = wasInLoop;
                       _inFunction = false;

                        return $"new Value(new Function({funcExpr.Args.Count}, {csharpName}, \"{name}\", ArgMode.Expected))";
                    }

                case BinaryExpr binaryExpr:
                    {
                        string left = CompileExpr(binaryExpr.Left);
                        string right = CompileExpr(binaryExpr.Right);
                        TokenType op = binaryExpr.Op;

                        if (op == TokenType.And)
                            return RuntimeOpToString("And", $"{left}, () => {right}");
                        else if (op == TokenType.Or)
                            return RuntimeOpToString("Or", $"{left}, () => {right}");

                        return op switch
                        {
                            TokenType.Add => RuntimeOpToString("Add", $"{left}, {right}", binaryExpr.Position),
                            TokenType.Sub => RuntimeOpToString("Sub", $"{left}, {right}", binaryExpr.Position),
                            TokenType.Mul => RuntimeOpToString("Mul", $"{left}, {right}", binaryExpr.Position),
                            TokenType.Div => RuntimeOpToString("Div", $"{left}, {right}", binaryExpr.Position),
                            TokenType.Mod => RuntimeOpToString("Mod", $"{left}, {right}", binaryExpr.Position),

                            TokenType.Less => RuntimeOpToString("Less", $"{left}, {right}", binaryExpr.Position),
                            TokenType.Greater => RuntimeOpToString("Greater", $"{left}, {right}", binaryExpr.Position),
                            TokenType.LessEq => RuntimeOpToString("LessEqual", $"{left}, {right}", binaryExpr.Position),
                            TokenType.GreaterEq => RuntimeOpToString("GreaterEqual", $"{left}, {right}", binaryExpr.Position),

                            TokenType.Equals => RuntimeOpToString("IsEqual", $"{left}, {right}"),
                            TokenType.NotEquals => RuntimeOpToString("NotEqual", $"{left}, {right}"),

                            _ => throw new Error("Invalid binary operator", binaryExpr.Position)
                        };
                    }

            }
            throw new Error("Invalid expression", expr.Position);
        }

        void BeginScope()
        {
            _indexLevel++;
            Scopes.Push(new Dictionary<string, string>());
        }

        void EndScope()
        {
            Scopes.Pop();
            _indexLevel--;
        }

        bool TryResolveLocal(string name, out string? local)
        {
            foreach (var scope in Scopes)
            {
                if (scope.TryGetValue(name, out local))
                    return true;
            }
            local = "";
            return false;
        }

        string ResolveLocal(string name, Position position)
        {
            if (TryResolveLocal(name, out string? local))
                return local!;
            throw new Error($"'{name}' does not exist", position);
        }

        string LocalToCsharpName(string name) => Scopes.Peek()[name];

        string SetLocal(string name, Position position)
        {
            if (Scopes.Peek().ContainsKey(name))
                throw new Error($"Variable '{name}' has already been declared", position);

            if (!NextLocalIndexs.ContainsKey(name))
                NextLocalIndexs[name] = 0;

            string csName = $"_{name}_{NextLocalIndexs[name]++}";

            Scopes.Peek().Add(name, csName);

            return csName;
        }

        void EmitLine(string line) => StringBuilder
            .Append(' ', _indexLevel * _indentSize)
            .AppendLine(line);

        string ToCSharpString(string str) => str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\t", "\\t")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\f", "\\f")
            .Replace("\a", "\\a")
            .Replace("\b", "\\b")
            .Replace("\e", "\\e")
            .Replace("\v", "\\v")
            .Replace("\0", "\\0");

        string RuntimeOpToString(string name, string data, Position position) =>
            $"RuntimeOperations.{name}({data}, {Runtime.PosToString(position)})";

        string RuntimeOpToString(string name, string data) =>
            $"RuntimeOperations.{name}({data})";

        void VerfiyStringArguments(List<string> strings, Func<string, string> message, Position position)
        {
            HashSet<string> hashSet = new HashSet<string>();
            for (int i = 0; i < strings.Count; i++)
                if (!hashSet.Add(strings[i]))
                    throw new Error(message(strings[i]), position);
        }

        FunctionInfo ResolveOverload(string name, int arity, Position position)
        {
            Functions.TryGetValue(name, out var overloads);
            if (!overloads!.TryGetValue(arity, out FunctionInfo? functionInfo))
                throw new Error("No overload of '{name}' accepts {arity} argument(s)", position);
            return functionInfo;
        }

        string FunctionInfoToString(FunctionInfo functionInfo, string name) => $"new Value(new Function({functionInfo.Arity}, {functionInfo.Name}, \"{name}\", ArgMode.Expected))"; 
    }
}
