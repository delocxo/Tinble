using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TinbleGenerated;

public struct Position
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

public class Error : Exception
{
    Position _position;

    public Error(string message, Position position) : base(message)
    {
        _position = position;
    }

    public void Print()
    {
        Console.Error.WriteLine($"Runtime Error: {_position.Source}:{_position.Line}:{_position.Column}: {Message}");
    }
}

public class ValueKeyComparer : IEqualityComparer<Value>
{
    public bool Equals(Value left, Value right)
    {
        if (left.Kind == ValueKind.String && right.Kind == ValueKind.String)
            return string.Equals(left.String, right.String, StringComparison.Ordinal);

        if (left.Kind == ValueKind.Int && right.Kind == ValueKind.Int)
            return left.Int == right.Int;

        if (left.Kind == ValueKind.Float && right.Kind == ValueKind.Float)
            return left.Float == right.Float;

        if (left.Kind == ValueKind.Int && right.Kind == ValueKind.Float)
            return IntEqualsFloat(left.Int, right.Float);

        if (left.Kind == ValueKind.Float && right.Kind == ValueKind.Int)
            return IntEqualsFloat(right.Int, left.Float);

        return false;
    }

    static bool IntEqualsFloat(long @int, double @float)
    {
        if (double.IsNaN(@float) || double.IsInfinity(@float))
            return false;

        if (@float != Math.Truncate(@float))
            return false;

        if (@float < long.MinValue || @float >= long.MaxValue)
            return false;

        return @int == (long)@float;
    }

    public int GetHashCode(Value value)
    {
        return value.Kind switch
        {
            ValueKind.String => HashCode.Combine(ValueKind.String, StringComparer.Ordinal.GetHashCode(value.String)),
            ValueKind.Int => HashCode.Combine("TinbleNumber", value.Int),
            ValueKind.Float => GetFloatHash(value.Float),
            _ => throw new UnreachableException()
        };
    }

    static int GetFloatHash(double value)
    {
        if (value == 0.0)
            value = 0.0;
        if (!double.IsInfinity(value) && value >= long.MinValue && value <= long.MaxValue && value == Math.Truncate(value))
            return HashCode.Combine("TinbleNumber", (long)value);
        return HashCode.Combine("TinbleNumber", value);
    }
}

public delegate Value FunctionDelegate(Value[] arguments, Position position);

public enum ArgMode
{
    Expected,
    Minimum,
    Range
}

public class Function
{
    public Function(int arity, FunctionDelegate @delegate, string name, ArgMode argMode)
    {
        Arity = arity;
        Delegate = @delegate;
        Name = name;
        ArgMode = argMode;
    }

    public Function(int arity, int maxArity, FunctionDelegate @delegate, string name, ArgMode argMode)
    {
        Arity = arity;
        MaxArity = maxArity;
        Delegate = @delegate;
        Name = name;
        ArgMode = argMode;
    }

    public int Arity { get; }
    public int MaxArity { get; }
    public FunctionDelegate Delegate { get; }
    public string Name { get; }
    public ArgMode ArgMode { get; }
}

public class Namespace
{
    public Namespace(string name)
    {
        Name = name;
    }

    public Value this[string name]
    {
        set => Values[name] = value;
    }

    public Dictionary<string, Value> Values { get; } = new Dictionary<string, Value>();
    public string Name { get; }
}

public class StructType
{
    public StructType(string name, Dictionary<string, int> fieldIndexes)
    {
        Name = name;
        FieldIndexes = fieldIndexes;
    }

    public string Name { get; }
    public Dictionary<string, int> FieldIndexes { get; }
    public Dictionary<string, Function> Methods { get; } = [];
}

public class StructInstance
{
    public StructInstance(StructType type, Value[] values)
    {
        Type = type;
        Values = values;
    }

    public StructType Type { get; }
    public Value[] Values { get; }
}

public class Dict
{
    public Dict(List<KeyValuePair<Value, Value>> keyValuePairs, Position position)
    {
        KeyValuePairs = new Dictionary<Value, Value>(new ValueKeyComparer());
        for (int i = 0; i < keyValuePairs.Count; i++)
        {
            if (KeyValuePairs.ContainsKey(keyValuePairs[i].Key))
                throw new Error("Duplicate dict key detected", position);
            KeyValuePairs.Add(keyValuePairs[i].Key, keyValuePairs[i].Value);
        }
    }

    public Dict(int capacity)
    {
        KeyValuePairs = new Dictionary<Value, Value>(capacity, new ValueKeyComparer());
    }

    public Dictionary<Value, Value> KeyValuePairs { get; }

    public Value GetValue(Value key, Position position)
    {
        ValidateKey(key, position);
        if (KeyValuePairs.TryGetValue(key, out Value value))
            return value;
        throw new Error($"Failed to find value from key", position);
    }

    public void SetValue(Value key, Value value, Position position)
    {
        ValidateKey(key, position);
        KeyValuePairs[key] = value;
    }

    public bool ContainsKey(Value key, Position position)
    {
        ValidateKey(key, position);
        return KeyValuePairs.ContainsKey(key);
    } 

    public bool TryGetValue(Value key, Position position, out Value value)
    {
        ValidateKey(key, position);
        return KeyValuePairs.TryGetValue(key, out value);
    }

    public bool Remove(Value key, Position position)
    {
        ValidateKey(key, position);
        if (KeyValuePairs.ContainsKey(key))
        {
            KeyValuePairs.Remove(key);
            return true;
        }
        return false;
    }
    
    void ValidateKey(Value key, Position position)
    {
        if (key.Kind is not (ValueKind.String or ValueKind.Int or ValueKind.Float))
            throw new Error("Dict keys must be strings, ints, or floats", position);
        if (key.Kind == ValueKind.Float && double.IsNaN(key.Float))
            throw new Error("NaN cannot be used as a dict key", position);
    }
}

public enum ValueKind
{
    Int,
    Float,
    String,
    Bool,
    Null,
    Function,
    Array,
    Namespace,
    StructType,
    StructInstance,
    Dict
}

public struct Value
{
    public ValueKind Kind { get; set; }
    public long Int { get; set; }
    public double Float { get; set; }
    public string String { get; set; }
    public bool Bool { get; set; }
    public object? Boxed { get; set; }
    public Function Function => (Function)Boxed!;
    #pragma warning disable CS9266 // Property accessor should use 'field' because the other accessor is using it.
    public List<Value> Array { get => (List<Value>)Boxed!; set => Boxed = value; }
    #pragma warning disable CS9266 // Property accessor should use 'field' because the other accessor is using it.
    public Namespace Namespace => (Namespace)Boxed!;
    public StructType StructType => (StructType)Boxed!;
    public StructInstance StructInstance => (StructInstance)Boxed!;
    public Dict Dict => (Dict)Boxed!;

    #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public Value(long value)
    {
        Kind = ValueKind.Int;
        Int = value;
    }

    public Value(double value)
    {
        Kind = ValueKind.Float;
        Float = value;
    }

    public Value(string value)
    {
        Kind = ValueKind.String;
        String = value;
    }
    public Value(bool value)
    {
        Kind = ValueKind.Bool;
        Bool = value;
    }

    public Value(Function function)
    {
        Kind = ValueKind.Function;
        Boxed = function;
    }

    public Value(List<Value> array)
    {
        Kind = ValueKind.Array;
        Boxed = array;
    }

    public Value(Namespace @namespace)
    {
        Kind = ValueKind.Namespace;
        Boxed = @namespace;
    }

    public Value(StructType structType)
    {
        Kind = ValueKind.StructType;
        Boxed = structType;
    }
    public Value(StructInstance structInstance)
    {
        Kind = ValueKind.StructInstance;
        Boxed = structInstance;
    }

    public Value(Dict dict)
    {
        Kind = ValueKind.Dict;
        Boxed = dict;
    }

    public Value(ValueKind kind)
    {
        Kind = kind;
    }

    #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public static Value Null() => new Value(ValueKind.Null);

    public override string ToString()
    {
        return Kind switch
        {
            ValueKind.Int => Int.ToString(),
            ValueKind.Float => Float.ToString(),
            ValueKind.String => String,
            ValueKind.Bool => $"{Bool}",
            ValueKind.Null => "null",
            ValueKind.Array => $"[{string.Join(", ", Array.Select(x =>
            {
                if (x.Kind == ValueKind.String)
                    return $"\'{x.String}\'";
                return x.ToString();
            }).ToList())}]",
            ValueKind.Function => $"{Function.Name}(...)",
            ValueKind.StructType => $"{StructType.Name} {{ {string.Join(", ", StructType.FieldIndexes.Keys)} }}",
            ValueKind.StructInstance => $"{StructInstance.Type.Name} {{ {string.Join(", ", StructInstance.Values.Select(x =>
            {
                if (x.Kind == ValueKind.String)
                    return $"\'{x.String}\'";
                return x.ToString();
            }).ToList())} }}",
            ValueKind.Dict => DictToString(),
            _ => "unknown type"
        };
    }

    string DictToString()
    {
        List<string> pairs = Dict.KeyValuePairs.Select(x =>
        {
            string key = "";
            string value = "";
            if (x.Key.Kind == ValueKind.String)
                key = $"\'{x.Key.String}\'";
            else
                key = x.Key.ToString();
            if (x.Value.Kind == ValueKind.String)
                value = $"\'{x.Value.String}\'";
            else
                value = x.Value.ToString();
            return $"{key}: {value}";

        }).ToList();

        return $"{{{string.Join(", ", pairs)}}}";
    }

    public Value Expect(ValueKind kind, string message, Position position)
    {
        if (Kind != kind)
            throw new Error(message, position);
        return this;
    }
}

public static class Natives
{
    public static Value RegisterString()
    {
        Namespace @namespace = new Namespace("string");

        @namespace["empty"] = new Value("");
        @namespace["newLine"] = new Value("\n");

        @namespace["join"] = new Value(
            new Function(
                2,
                (args, pos) =>
                {
                    Value separator = args[0]
                        .Expect(ValueKind.String, "Expected string for join separator", pos);
                    Value array = args[1]
                        .Expect(ValueKind.Array, "Expected array for join", pos);
                    return new Value(string.Join(separator.String, array.Array));

                },
                "join",
                ArgMode.Expected
            )
        );

        @namespace["repeat"] = new Value(
            new Function(
                2,
                (args, pos) =>
                {
                    Value toRepeat = args[0]
                        .Expect(ValueKind.String, "Expected string for repeat", pos);
                    Value amount = args[1]
                        .Expect(ValueKind.Int, "Expected an integer for repeat count", pos);
                    StringBuilder sb = new StringBuilder();
                    for (long i = 0; i < amount.Int; i++)
                        sb.Append(toRepeat.String);
                    return new Value(sb.ToString());

                },
                "repeat",
                ArgMode.Expected
            )
        );

        return new Value(@namespace);
    }

    public static Value RegisterInt()
    {
        Namespace @namespace = new Namespace("int");

        @namespace["minValue"] = new Value(long.MinValue);
        @namespace["maxValue"] = new Value(long.MaxValue);

        @namespace["parse"] = new Value(
            new Function(
                1,
                (args, pos) =>
                {
                    Value toBeParsed = args[0];
                    if (toBeParsed.Kind == ValueKind.Int)
                        return toBeParsed;
                    else if (toBeParsed.Kind == ValueKind.Float)
                        return new Value((long)toBeParsed.Float);
                    else if (toBeParsed.Kind == ValueKind.Bool)
                        return new Value(toBeParsed.Bool ? 1L : 0L);
                    else if (toBeParsed.Kind == ValueKind.String)
                        if (long.TryParse(toBeParsed.String, out long result))
                            return new Value(result);
                        else
                            throw new Error($"Failed to cast '{toBeParsed.String}' to an int", pos);
                    throw new Error($"{toBeParsed.Kind} cannot be casted to an int", pos);
                },
                "parse",
                ArgMode.Expected
            )
        );

        @namespace["tryParse"] = new Value(
            new Function(
                1,
                (args, pos) =>
                {
                    Value toBeParsed = args[0];
                    if (toBeParsed.Kind == ValueKind.Int)
                        return toBeParsed;
                    else if (toBeParsed.Kind == ValueKind.Float)
                        return new Value((long)toBeParsed.Float);
                    else if (toBeParsed.Kind == ValueKind.Bool)
                        return new Value(toBeParsed.Bool ? 1L : 0L);
                    else if (toBeParsed.Kind == ValueKind.String)
                        if (long.TryParse(toBeParsed.String, out long result))
                            return new Value(result);
                        else
                            return Value.Null();
                    return Value.Null();
                },
                "tryParse",
                ArgMode.Expected
            )
        );

        @namespace["isInt"] = new Value(
            new Function(
                1,
                (args, pos) =>
                {
                    return new Value(args[0].Kind == ValueKind.Int);
                },
                "isInt",
                ArgMode.Expected
            )
        );

        return new Value(@namespace);
    }

    public static Value RegisterFloat()
    {
        Namespace @namespace = new Namespace("float");

        @namespace["minValue"] = new Value(double.MinValue);
        @namespace["maxValue"] = new Value(double.MaxValue);
        @namespace["positiveInfinity"] = new Value(double.PositiveInfinity);
        @namespace["negativeInfinity"] = new Value(double.NegativeInfinity);

        @namespace["parse"] = new Value(
            new Function(
                1,
                (args, pos) =>
                {
                    Value toBeParsed = args[0];
                    if (toBeParsed.Kind == ValueKind.Int)
                        return new Value((double)toBeParsed.Int);
                    else if (toBeParsed.Kind == ValueKind.Float)
                        return toBeParsed;
                    else if (toBeParsed.Kind == ValueKind.Bool)
                        return new Value(toBeParsed.Bool ? 1D : 0D);
                    else if (toBeParsed.Kind == ValueKind.String)
                        if (double.TryParse(toBeParsed.String, out double result))
                            return new Value(result);
                        else
                            throw new Error($"Failed to cast '{toBeParsed.String}' to a float", pos);
                    throw new Error($"{toBeParsed.Kind} cannot be casted to a float", pos);
                },
                "parse",
                ArgMode.Expected
            )
        );

        @namespace["tryParse"] = new Value(
            new Function(
                1,
                (args, pos) =>
                {
                    Value toBeParsed = args[0];
                    if (toBeParsed.Kind == ValueKind.Int)
                        return new Value((double)toBeParsed.Int);
                    else if (toBeParsed.Kind == ValueKind.Float)
                        return toBeParsed;
                    else if (toBeParsed.Kind == ValueKind.Bool)
                        return new Value(toBeParsed.Bool ? 1D : 0D);
                    else if (toBeParsed.Kind == ValueKind.String)
                        if (double.TryParse(toBeParsed.String, out double result))
                            return new Value(result);
                        else
                            return Value.Null();
                    return Value.Null();
                },
                "tryParse",
                ArgMode.Expected
            )
        );

        @namespace["isFloat"] = new Value(
            new Function(
                1,
                (args, pos) =>
                {
                    return new Value(args[0].Kind == ValueKind.Float);
                },
                "isFloat",
                ArgMode.Expected
            )
        );

        return new Value(@namespace);
    }

    public static Value RegisterArray()
    {
        Namespace @namespace = new Namespace("array");

        @namespace["new"] = new Value(
            new Function(
                0,
                1,
                (args, pos) =>
                {
                    if (args.Length == 0)
                        return new Value(new List<Value>());
                    Value capacity = args[0]
                        .Expect(ValueKind.Int, "Expected int capacity", pos);
                    if (capacity.Int < 0)
                        throw new Error("Capacity cannot be negative", pos);
                    if (capacity.Int > int.MaxValue)
                        throw new Error("Capacity is too large", pos);
                    return new Value(new List<Value>((int)capacity.Int));
                },
                "new",
                ArgMode.Range
            )
        );

        @namespace["repeat"] = new Value(
            new Function(
                2,
                (args, pos) =>
                {
                    Value toBeRepeated = args[0];
                    Value amount = args[1]
                        .Expect(ValueKind.Int, "Expected int repeat amount", pos);
                    if (amount.Int < 0)
                        throw new Error("Repeat count cannot be negative", pos);
                    if (amount.Int > int.MaxValue)
                        throw new Error("Repeat count is too large", pos);
                    List<Value> newArray = new List<Value>((int)amount.Int);
                    for (int i = 0; i < amount.Int; i++)
                        newArray.Add(toBeRepeated);
                    return new Value(newArray);
                },
                "repeat",
                ArgMode.Expected
            )
        );

        return new Value(@namespace);
    }

    public static Value RegisterDict()
    {
        Namespace @namespace = new Namespace("dict");

        @namespace["new"] = new Value(
            new Function(
                0,
                1,
                (args, pos) =>
                {
                    if (args.Length == 0)
                        return new Value(new Dict([], pos));
                    Value capacity = args[0]
                        .Expect(ValueKind.Int, "Expected int capacity", pos);
                    if (capacity.Int < 0)
                        throw new Error("Capacity cannot be negative", pos);
                    if (capacity.Int > int.MaxValue)
                        throw new Error("Capacity is too large", pos);
                    Dict dict = new Dict((int)capacity.Int);
                    return new Value();
                },
                "new",
                ArgMode.Range
            )
        );

        return new Value(@namespace);
    }
}

public static class RuntimeOperations
{
    public static Dictionary<string, Value> Globals = new Dictionary<string, Value>()
    {
        {
            "print",
            new Value(
                new Function(
                    1,
                    (args, pos) =>
                    {
                        Console.Write(args[0]);
                        return Value.Null();
                    },
                    "print",
                    ArgMode.Expected
                )
            )
        },
        {
            "println",
            new Value(
                new Function(
                    1,
                    (args, pos) =>
                    {
                        Console.WriteLine(args[0]);
                        return Value.Null();
                    },
                    "println",
                    ArgMode.Expected
                )
            )
        },
        {
            "input",
            new Value(
                new Function(
                    0,
                    (args, pos) =>
                    {
                        return new Value(Console.ReadLine() ?? "");
                    },
                    "input",
                    ArgMode.Expected
                )
            )
        },
        {
            "format",
            new Value(
                new Function(
                    1,
                    (args, pos) =>
                    {
                        var sb = new StringBuilder();
                        if (args[0].Kind != ValueKind.String)
                            throw new Error("Expected string template", pos);
                        string template = args[0].String;
                        ReadOnlySpan<char> span = template.AsSpan();
                        int currentArg = 1;
                        for (int i = 0; i < span.Length; i++)
                        {
                            if (i + 1 < span.Length && span[i] == '$' && span[i + 1] == '$')
                            {
                                sb.Append('$');
                                i++;
                                continue;
                            }
                            else if (span[i] == '$')
                            {
                                if (currentArg >= args.Length)
                                    throw new Error("Missing argument(s) for format", pos);
                                sb.Append(args[currentArg++]);
                                continue;
                            }
                            sb.Append(span[i]);
                        }
                        if (currentArg < args.Length)
                            throw new Error($"Too many arguments for format", pos);
                        return new Value(sb.ToString());
                    },
                    "format",
                    ArgMode.Minimum
                )
            )
        },
        {
            "string",
            Natives.RegisterString()
        },
        {
            "int",
            Natives.RegisterInt()
        },
        {
            "float",
            Natives.RegisterFloat()
        },
        {
            "array",
            Natives.RegisterArray()
        },
        {
            "dict",
            Natives.RegisterDict()
        },
        {
            "KeyValue",
            new Value(new StructType("KeyValue", new Dictionary<string, int>()
            {
                { "key", 0 },
                { "value", 1 }
            } ))
        },
        {
            "Result",
            new Value(new StructType("Result", new Dictionary<string, int>()
            {
                { "success", 0 },
                { "value", 1 }
            } ))
        }
    };

    public static void RegisterStruct(StructType structType, Position position)
    {
        if (Globals.TryGetValue(structType.Name, out Value value))
            if (value.Kind == ValueKind.StructType)
                throw new Error($"'{structType.Name}' is an already existing struct", position);
        Globals.Add(structType.Name, new Value(structType));
    }

    public static Value Add(Value left, Value right, Position position)
    {
        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Int, ValueKind.Int) => new Value(unchecked(left.Int + right.Int)),
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float + right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float + right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int + right.Float),
            (ValueKind.String, ValueKind.String) => new Value(left.String + right.String),
            _ => throw ThrowBinaryError("+", left, right, position)
        };
    }

    public static Value Sub(Value left, Value right, Position position)
    {
        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Int, ValueKind.Int) => new Value(unchecked(left.Int - right.Int)),
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float - right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float - right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int - right.Float),
            _ => throw ThrowBinaryError("-", left, right, position)
        };
    }
    public static Value Mul(Value left, Value right, Position position)
    {
        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Int, ValueKind.Int) => new Value(unchecked(left.Int * right.Int)),
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float * right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float * right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int * right.Float),
            _ => throw ThrowBinaryError("*", left, right, position)
        };
    }
    public static Value Div(Value left, Value right, Position position)
    {
        if (left.Kind == ValueKind.Int && right.Kind == ValueKind.Int)
        {
            if (left.Int == long.MinValue && right.Int == -1)
                throw new Error("Division failure", position);

            if (right.Int == 0)
                throw new Error("Division by zero", position);

            return new Value(left.Int / right.Int);
        }

        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float / right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float / right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int / right.Float),
            _ => throw ThrowBinaryError("/", left, right, position)
        };
    }

    public static Value Mod(Value left, Value right, Position position)
    {
        if (left.Kind == ValueKind.Int && right.Kind == ValueKind.Int)
        {
            if (left.Int == long.MinValue && right.Int == -1)
                throw new Error("Modulo failure", position);

            if (right.Int == 0)
                throw new Error("Modulo by zero", position);

            return new Value(left.Int % right.Int);
        }

        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float % right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float % right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int % right.Float),
            _ => throw ThrowBinaryError("%", left, right, position)
        };
    }

    public static Value Less(Value left, Value right, Position position)
    {
        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Int, ValueKind.Int) => new Value(left.Int < right.Int),
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float < right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float < right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int < right.Float),
            _ => throw ThrowBinaryError("<", left, right, position)
        };
    }

    public static Value Greater(Value left, Value right, Position position)
    {
        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Int, ValueKind.Int) => new Value(left.Int > right.Int),
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float > right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float > right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int > right.Float),
            _ => throw ThrowBinaryError(">", left, right, position)
        };
    }

    public static Value LessEqual(Value left, Value right, Position position)
    {
        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Int, ValueKind.Int) => new Value(left.Int <= right.Int),
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float <= right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float <= right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int <= right.Float),
            _ => throw ThrowBinaryError("<=", left, right, position)
        };
    }

    public static Value GreaterEqual(Value left, Value right, Position position)
    {
        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Int, ValueKind.Int) => new Value(left.Int >= right.Int),
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float >= right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float >= right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int >= right.Float),
            _ => throw ThrowBinaryError(">=", left, right, position)
        };
    }

    public static Value IsEqual(Value left, Value right)
    {
        return (left.Kind, right.Kind) switch
        {
            (ValueKind.Int, ValueKind.Int) => new Value(left.Int == right.Int),
            (ValueKind.Float, ValueKind.Int) => new Value(left.Float == right.Int),
            (ValueKind.Float, ValueKind.Float) => new Value(left.Float == right.Float),
            (ValueKind.Int, ValueKind.Float) => new Value(left.Int == right.Float),
            (ValueKind.String, ValueKind.String) => new Value(left.String == right.String),
            (ValueKind.Bool, ValueKind.Bool) => new Value(left.Bool == right.Bool),
            (ValueKind.Null, ValueKind.Null) => new Value(true),
            (ValueKind.Function, ValueKind.Function) => new Value(left.Function.Name == right.Function.Name),
            (ValueKind.Array, ValueKind.Array) => new Value(left.Array == right.Array),
            (ValueKind.StructType, ValueKind.StructType) => new Value(left.StructType == right.StructType),
            (ValueKind.StructInstance, ValueKind.StructInstance) => new Value(left.StructInstance == right.StructInstance),
            (ValueKind.Dict, ValueKind.Dict) => new Value(left.Dict == right.Dict),
            _ => new Value(false)
        };
    }

    public static Value Negate(Value left, Position position)
    {
        if (left.Kind == ValueKind.Int)
            return new Value(-left.Int);
        else if (left.Kind == ValueKind.Float)
            return new Value(-left.Float);
        throw new Error($"Cannot apply '-' to {left.Kind}", position);
    }

    public static Value Flip(Value left, Position position)
    {
        if (left.Kind == ValueKind.Bool)
            return new Value(!left.Bool);
        throw new Error($"Cannot apply '!' to {left.Kind}", position);
    }

    public static Value And(Value left, Func<Value> right)
    {
        return IsTruthy(left) ? right() : left;
    }

    public static Value Or(Value left, Func<Value> right)
    {
        return IsTruthy(left) ? left : right();
    }

    public static Value NotEqual(Value left, Value right) => new Value(!IsEqual(left, right).Bool);

    public static Value IndexGet(Value target, Value index, Position position)
    {
        if (target.Kind is not (ValueKind.Array or ValueKind.String or ValueKind.Dict))
            throw new Error($"Type {target.Kind} cannot be indexed read", position);

        if (target.Kind == ValueKind.Dict)
            return target.Dict.GetValue(index, position);

        if (index.Kind != ValueKind.Int)
            throw new Error($"Type {target.Kind} cannot be used as an indexer", position);

        long rawIndex = index.Int;

        if (rawIndex < 0)
            throw new Error($"Index cannot be below zero", position);

        int actualIndex = (int)rawIndex;

        if (target.Kind == ValueKind.Array)
        {
            List<Value> array = target.Array;

            if (actualIndex >= array.Count)
                throw new Error($"Array index {actualIndex} exceeds length {array.Count}", position);

            return array[actualIndex];
        }
        else
        {
            string str = target.String;

            if (actualIndex >= str.Length)
                throw new Error($"String index {actualIndex} exceeds length {str.Length}", position);

            return new Value(str[actualIndex].ToString());
        }
    }

    public static void IndexSet(Value target, Value index, Value value, Position position)
    {
        if (target.Kind is not (ValueKind.Array or ValueKind.Dict))
            throw new Error($"Type {target.Kind} cannot be indexed set", position);

        if (target.Kind == ValueKind.Dict)
        {
            target.Dict.SetValue(index, value, position);
            return;
        }

        if (index.Kind != ValueKind.Int)
            throw new Error($"Type {target.Kind} cannot be used as an indexer", position);

        long rawIndex = index.Int;

        if (rawIndex < 0)
            throw new Error($"Index cannot be below zero", position);

        int actualIndex = (int)rawIndex;

        List<Value> array = target.Array;

        if (actualIndex >= array.Count)
            throw new Error($"Array index {actualIndex} exceeds length {array.Count}", position);

        array[actualIndex] = value;
    }

    public static Value MemberGet(Value target, string name, Position position)
    {
        if (target.Kind == ValueKind.Array)
        {
            return name switch
            {
                "push" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            target.Array.Add(args[0]);
                            return Value.Null();
                        },
                        "push",
                        ArgMode.Expected
                    )
                ),
                "length" => new Value(target.Array.Count),
                "isEmpty" => new Value(target.Array.Count == 0),
                "pop" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            if (target.Array.Count == 0)
                                throw new Error("Cannot pop an empty array", position);
                            Value last = target.Array[^1];
                            target.Array.RemoveAt(target.Array.Count - 1);
                            return last;
                        },
                        "pop",
                        ArgMode.Expected
                    )
                ),
                "contains" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            for (int i = 0; i < target.Array.Count; i++)
                            {
                                if (IsEqual(args[0], target.Array[i]).Bool)
                                    return new Value(true);
                            }
                            return new Value(false);
                        },
                        "contains",
                        ArgMode.Expected
                    )
                ),
                "indexOf" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            for (int i = 0; i < target.Array.Count; i++)
                            {
                                if (IsEqual(args[0], target.Array[i]).Bool)
                                    return new Value(i);
                            }
                            return new Value(-1L);
                        },
                        "indexOf",
                        ArgMode.Expected
                    )
                ),
                "remove" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            for (int i = target.Array.Count - 1; i >= 0; i--)
                            {
                                if (IsEqual(args[0], target.Array[i]).Bool)
                                {
                                    target.Array.RemoveAt(i);
                                    return new Value(true);
                                }
                            }
                            return new Value(false);
                        },
                        "remove",
                        ArgMode.Expected
                    )
                ),
                "removeAt" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            Value index = args[0];

                            if (index.Kind != ValueKind.Int)
                                throw new Error($"Type {target.Kind} cannot be used as a remove index", position);

                            long rawIndex = index.Int;

                            if (rawIndex < 0)
                                throw new Error($"Remove index cannot be below zero", position);

                            int actualIndex = (int)rawIndex;

                            if (actualIndex >= target.Array.Count)
                                throw new Error($"Array remove index {actualIndex} exceeds length {target.Array.Count}", position);

                            target.Array.RemoveAt(actualIndex);

                            return Value.Null();
                        },
                        "removeAt",
                        ArgMode.Expected
                    )
                ),
                "clear" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            target.Array.Clear();
                            return new Value(false);
                        },
                        "clear",
                        ArgMode.Expected
                    )
                ),
                "reverse" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            target.Array.Reverse();
                            return target;
                        },
                        "reverse",
                        ArgMode.Expected
                    )
                ),
                "copy" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            List<Value> copied = [.. target.Array];
                            return new Value(copied);
                        },
                        "copy",
                        ArgMode.Expected
                    )
                ),
                "copyTo" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            args[0].Array = [.. target.Array];
                            return Value.Null();
                        },
                        "copyTo",
                        ArgMode.Expected
                    )
                ),
                "addRange" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            target.Array.AddRange(args[0].Array);
                            return Value.Null();
                        },
                        "addRange",
                        ArgMode.Expected
                    )
                ),
                "shuffle" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            target.Array.Shuffle();
                            return target;
                        },
                        "shuffle",
                        ArgMode.Expected
                    )
                ),
                _ => throw new Error($"Array does not have member '{name}'", position)
            };
        }
        else if (target.Kind == ValueKind.String)
        {
            return name switch
            {
                "length" => new Value(target.String.Length),
                "isEmpty" => new Value(target.String.Length == 0 || string.IsNullOrWhiteSpace(target.String)),
                "trim" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            return new Value(target.String.Trim());
                        },
                        "trim",
                        ArgMode.Expected
                    )
                ),
                "trimStart" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            return new Value(target.String.TrimStart());
                        },
                        "trimStart",
                        ArgMode.Expected
                    )
                ),
                "trimEnd" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            return new Value(target.String.TrimEnd());
                        },
                        "trimEnd",
                        ArgMode.Expected
                    )
                ),
                "toLower" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            return new Value(target.String.ToLower());
                        },
                        "toLower",
                        ArgMode.Expected
                    )
                ),
                "toUpper" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            return new Value(target.String.ToUpper());
                        },
                        "toUpper",
                        ArgMode.Expected
                    )
                ),
                "contains" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            Value needle = args[0]
                                .Expect(ValueKind.String, "Expected string as the needle", pos);
                            return new Value(target.String.Contains(needle.String));
                        },
                        "contains",
                        ArgMode.Expected
                    )
                ),
                "indexOf" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            Value needle = args[0]
                                .Expect(ValueKind.String, "Expected string as the needle", pos);
                            return new Value(target.String.IndexOf(needle.String));
                        },
                        "indexOf",
                        ArgMode.Expected
                    )
                ),
                "startsWith" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            Value needle = args[0]
                                .Expect(ValueKind.String, "Expected string as the needle", pos);
                            return new Value(target.String.StartsWith(needle.String));
                        },
                        "startsWith",
                        ArgMode.Expected
                    )
                ),
                "endsWith" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            Value needle = args[0]
                                .Expect(ValueKind.String, "Expected string as the needle", pos);
                            return new Value(target.String.EndsWith(needle.String));
                        },
                        "endsWith",
                        ArgMode.Expected
                    )
                ),
                "replace" => new Value(
                    new Function(
                        2,
                        (args, pos) =>
                        {
                            Value old = args[0]
                                .Expect(ValueKind.String, "Expected string for old", pos);
                            Value @new = args[1]
                                .Expect(ValueKind.String, "Expected string for new", pos);
                            return new Value(target.String.Replace(old.String, @new.String));
                        },
                        "replace",
                        ArgMode.Expected
                    )
                ),
                "split" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            Value separator = args[0]
                                .Expect(ValueKind.String, "Expected string for separator", pos);
                            string[] split = target.String.Split(separator.String);
                            List<Value> splitLines = new List<Value>(split.Length);
                            for (int i = 0; i < split.Length; i++)
                                splitLines.Add(new Value(split[i]));
                            return new Value(splitLines);
                        },
                        "split",
                        ArgMode.Expected
                    )
                ),
                "slice" => new Value(
                   new Function(
                       2,
                       (args, pos) =>
                       {
                           Value start = args[0]
                                .Expect(ValueKind.Int, "Expected an int for the start", pos);
                           Value end = args[1]
                                .Expect(ValueKind.Int, "Expected an int for the end", pos);
                           if (start.Int < 0 || end.Int < 0
                           || start.Int > target.String.Length
                           || end.Int > target.String.Length)
                               throw new Error("Start index or end index is out of range", pos);
                           if (end.Int < start.Int)
                               throw new Error("End index cannot be smaller than the start index", pos);
                           return new Value(target.String[(int)start.Int..(int)end.Int]);
                       },
                       "slice",
                       ArgMode.Expected
                   )
               ),
                _ => throw new Error($"String does not have member '{name}'", position)
            };
        }
        else if (target.Kind == ValueKind.Dict)
        {
            var keyValuePairs = target.Dict.KeyValuePairs;
            return name switch
            {
                "length" => new Value(keyValuePairs.Count),
                "isEmpty" =>new Value(keyValuePairs.Count == 0),
                "containsKey" => new Value(
                   new Function(
                       1,
                       (args, pos) =>
                       {
                           return new Value(target.Dict.ContainsKey(args[0], pos));
                       },
                       "containsKey",
                       ArgMode.Expected
                   )
               ),
                "containsValue" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            return new Value(keyValuePairs.ContainsValue(args[0]));
                        },
                        "containsValue",
                        ArgMode.Expected
                    )
                ),
                "get" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            if (target.Dict.TryGetValue(args[0], pos, out Value value))
                                return MakeResultStruct(true, value);
                            return MakeResultStruct(false, Value.Null());
                        },
                        "get",
                        ArgMode.Expected
                    )
                ),
                "remove" => new Value(
                    new Function(
                        1,
                        (args, pos) =>
                        {
                            return new Value(target.Dict.Remove(args[0], pos));
                        },
                        "remove",
                        ArgMode.Expected
                    )
                ),
                "clear" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            keyValuePairs.Clear();
                            return Value.Null();
                        },
                        "clear",
                        ArgMode.Expected
                    )
                ),
                "keys" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            return new Value(keyValuePairs.Keys.ToList());
                        },
                        "keys",
                        ArgMode.Expected
                    )
                ),
                "values" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            return new Value(keyValuePairs.Values.ToList());
                        },
                        "values",
                        ArgMode.Expected
                    )
                ),
                "copy" => new Value(
                    new Function(
                        0,
                        (args, pos) =>
                        {
                            return new Value(new Dict(keyValuePairs.ToList(), pos));
                        },
                        "copy",
                        ArgMode.Expected
                    )
                ),
               // "addRange" => new Value(
               //    new Function(
               //        1,
               //        (args, pos) =>
               //        {
               //            Value other = args[0]
               //                 .Expect(ValueKind.Dict, "Expected a dict for appending", pos);
               //            foreach (var otherPair in other.Dict.KeyValuePairs)
               //                keyValuePairs.Add(otherPair.Key, otherPair.Value);
               //            return Value.Null();
               //        },
               //        "addRange",
               //        ArgMode.Expected
               //    )
               //),
                _ => throw new Error($"Dict does not have member '{name}'", position)
            };
        }
        else if (target.Kind == ValueKind.Namespace)
        {
            if (target.Namespace.Values.TryGetValue(name, out Value value))
                return value;
            throw new Error($"Namespace {target.Namespace.Name} does not contain member '{name}'", position);
        }
        else if (target.Kind == ValueKind.StructInstance)
        {
            if (target.StructInstance.Type.FieldIndexes.TryGetValue(name, out int index))
                return target.StructInstance.Values[index];
            if (target.StructInstance.Type.Methods.TryGetValue(name, out Function? function))
                return BindMethod(target, function, position);
            throw new Error($"Struct {target.StructInstance.Type.Name} does not contain member '{name}'", position);
        }
        else if (target.Kind == ValueKind.StructType)
        {
            if (name == "register")
                return CreatRegisterFunction(target.StructType);
            throw new Error($"Struct '{target.StructType.Name}' does not have member '{name}'", position);
        }
        throw new Error($"Type {target.Kind} cannot be member accessed", position);
    }

    static Value CreatRegisterFunction(StructType structType)
    {
        Function register = new Function(
            2,
            (args, position) =>
            {
                Value nameValue = args[0]
                    .Expect(ValueKind.String, "Expected method name", position);

                Value functionValue = args[1]
                    .Expect(ValueKind.Function, "Expected function to regiser", position);

                string methodName = nameValue.String;
                Function method = functionValue.Function;

                if (structType.FieldIndexes.ContainsKey(methodName))
                    throw new Error($"'{methodName}' is already a field inside '{structType.Name}'", position);

                if (structType.Methods.ContainsKey(methodName))
                    throw new Error($"'{methodName}' is already registered inside '{structType.Name}'", position);

                structType.Methods.Add(methodName, method);

                return Value.Null();
            },
            "register",
            ArgMode.Expected
        );

        return new Value(register);
    }

    static Value BindMethod(Value self, Function method, Position position)
    {
        int boundArity = 0;
        int boundMaxArity = 0;

        switch (method.ArgMode)
        {
            case ArgMode.Expected:
                if (method.Arity < 1)
                    throw new Error($"Method '{method.Name}' must accept self.", position);
                boundArity = method.Arity - 1;
                break;

            case ArgMode.Minimum:
                boundArity = Math.Max(0, method.Arity - 1);
                break;

            case ArgMode.Range:
                if (method.MaxArity < 1)
                    throw new Error($"Method '{method.Name}' cannot accept self", position);
                boundArity = Math.Max(0, method.Arity - 1);
                boundMaxArity = method.MaxArity - 1;
                break;
        }

        Function boundMethod = new Function(
            boundArity,
            boundMaxArity,
            (args, callPosition) =>
            {
                Value[] actualArgs = new Value[args.Length + 1];
                actualArgs[0] = self;
                Array.Copy(args, 0, actualArgs, 1, args.Length);
                return method.Delegate(actualArgs, callPosition);
            },
            method.Name,
            method.ArgMode
        );

        return new Value(boundMethod);
    }

    public static void MemberSet(Value target, Value value, string name, Position position)
    {
        if (target.Kind == ValueKind.StructInstance)
        {
            if (target.StructInstance.Type.FieldIndexes.TryGetValue(name, out int index))
            {
                target.StructInstance.Values[index] = value;
                return;
            }
            throw new Error($"Struct {target.StructInstance.Type.Name} does not contain member '{name}'", position);

        }
        throw new Error($"Type {target.Kind} cannot be member set", position);
    }

    public static Value GetName(string name, Position position)
    {
        if (Globals.TryGetValue(name, out Value value))
            return value;
        throw new Error($"'{name}' does not exist", position);
    }

    public static bool IsTruthy(Value value)
    {
        if (value.Kind == ValueKind.StructInstance)
            if (value.StructInstance.Type.Name == "Result")
                return value.StructInstance.Values[0].Bool;

        return value.Kind switch
        {
            ValueKind.Null => false,
            ValueKind.Bool => value.Bool,
            _ => true
        };
    }

    public static List<Value> GetIterable(Value value, Position position)
    {
        if (value.Kind is not (ValueKind.Array or ValueKind.String or ValueKind.Dict))
            throw new Error($"{value.Kind} cannot be iterated over", position);
        if (value.Kind == ValueKind.Array)
            return value.Array;
        else if (value.Kind == ValueKind.String)
        {
            List<Value> values = new List<Value>(value.String.Length);
            for (int i = 0; i < value.String.Length; i++)
                values.Add(new Value(value.String[i].ToString()));
            return values;
        }
        else
        {
            List<Value> values = new List<Value>(value.Dict.KeyValuePairs.Count);
            foreach (var keyValuePair in value.Dict.KeyValuePairs)
                values.Add(new Value(new StructInstance(Globals["KeyValue"].StructType, [keyValuePair.Key, keyValuePair.Value])));
            return values;
        }
    }

    static Error ThrowBinaryError(string op, Value left, Value right, Position position)
    {
        return new Error($"Cannot apply '{op}' to {left.Kind} and {right.Kind}]", position);
    }

    static Value MakeStructInstance(StructType structType, Value[] values)
    {
        return new Value(new StructInstance(structType, values));
    }

    static Value MakeResultStruct(bool success, Value value)
    {
        return MakeStructInstance(Globals["Result"].StructType, [new Value(success), value]);
    }

    public static Value Call(Value target, Value[] args, Position position)
    {
        if (target.Kind == ValueKind.Function)
        {
            Function function = target.Function;

            switch (function.ArgMode)
            {
                case ArgMode.Expected:
                    if (function.Arity != args.Length)
                        throw new Error($"'{function.Name}' expects {function.Arity} argument(s), got {args.Length}", position);
                    break;

                case ArgMode.Minimum:
                    if (args.Length < function.Arity)
                        throw new Error($"'{function.Name}' expects atleast {function.Arity} argument(s), got {args.Length}", position);
                    break;

                case ArgMode.Range:
                    if (args.Length < function.Arity || args.Length > function.MaxArity)
                        throw new Error($"'{function.Name}' expects between {function.Arity} and {function.MaxArity} arguments, got {args.Length}", position);
                    break;
            }

            return function.Delegate(args, position);
        }
        else if (target.Kind == ValueKind.StructType)
        {
            StructType structType = target.StructType;

            if (structType.FieldIndexes.Count != args.Length)
                throw new Error($"'{structType.Name}' expects {structType.FieldIndexes.Count} argument(s), got {args.Length}", position);

            return new Value(new StructInstance(structType, args));
        }
        throw new Error($"{target.Kind} is not callable", position);
    }
}

