using System;
using System.Collections.Generic;
using System.Text;

namespace Tinble
{
    internal class Lexer
    {
        string _code;
        int _i = 0;
        Position _currentPosition;

        public static Dictionary<string, TokenType> Keywords { get; } = new Dictionary<string, TokenType>()
        {
            { "make", TokenType.Make },
            { "if", TokenType.If },
            { "else", TokenType.Else },
            { "elseif", TokenType.Elseif },
            { "true", TokenType.True },
            { "false", TokenType.False },
            { "null", TokenType.Null },
            { "while", TokenType.While },
            { "break", TokenType.Break },
            { "continue", TokenType.Continue },
            { "return", TokenType.Return },
            { "for", TokenType.For },
            { "in", TokenType.In },
            { "import", TokenType.Import },
            { "dict", TokenType.Dict },
            { "enum", TokenType.Enum }
        };

        public static Dictionary<string, TokenType> Symbols { get; } = new Dictionary<string, TokenType>()
        {
            { "+", TokenType.Add },
            { "-", TokenType.Sub },
            { "*", TokenType.Mul },
            { "/", TokenType.Div },
            { "%", TokenType.Mod },
            { "<", TokenType.Less },
            { ">", TokenType.Greater },
            { "<=", TokenType.LessEq },
            { ">=", TokenType.GreaterEq },
            { "==", TokenType.Equals },
            { "!=", TokenType.NotEquals },
            { "!", TokenType.Bang },
            { "=", TokenType.Equal },
            { "{", TokenType.LeftBrace },
            { "}", TokenType.RightBrace },
            { ";", TokenType.Semicolon },
            { "(", TokenType.LeftParen },
            { ")", TokenType.RightParen },
            { "&&", TokenType.And },
            { "||", TokenType.Or },
            { ",", TokenType.Comma },
            { "[", TokenType.LeftBracket },
            { "]", TokenType.RightBracket },
            { ".", TokenType.Dot },
            { ":", TokenType.Colon }
        };

        public static string? GetKeywordFromType(TokenType type)
        {
            KeyValuePair<string, TokenType>? value = Keywords.FirstOrDefault(x => x.Value == type);
            if (value == null)
                return null;
            return value.Value.Key;
        }

        public static string? GetSymbolFromType(TokenType type)
        {
            KeyValuePair<string, TokenType>? value = Symbols.FirstOrDefault(x => x.Value == type);
            if (value == null)
                return null;
            return value.Value.Key;
        }

        public Lexer(string source)
        {
            _code = File.ReadAllText(source);

            _currentPosition = new Position(1, 1, source);
        }

        public List<Token> Tokenize()
        {
            List<Token> tokens = new List<Token>();

            while (NotAtEnd())
            {
                if (Current() == '"')
                {
                    tokens.Add(LexString());
                    continue;
                }

                if (char.IsLetter(Current()))
                {
                    tokens.Add(LexAlpha());
                    continue;
                }

                if (char.IsDigit(Current()))
                {
                    tokens.Add(LexDigit());
                    continue;
                }

                if (NotAtEnd(1))
                {
                    string value = $"{Current()}{Current(1)}";
                    if (Symbols.TryGetValue(value, out TokenType fat))
                    {
                        tokens.Add(new Token(value, fat, _currentPosition));
                        Next();
                        Next();
                        continue;
                    }
                }

                if (Symbols.TryGetValue(Current().ToString(), out TokenType single))
                {
                    tokens.Add(new Token(Current().ToString(), single, _currentPosition));
                    Next();
                    continue;
                }

                Next();
            }

            tokens.Add(new Token("End of File", TokenType.EOF, _currentPosition));
            return tokens;
        }

        Token LexString()
        {
            Position pos = _currentPosition;

            Next();

            StringBuilder sb = new StringBuilder();

            while (NotAtEnd() && Current() != '"')
            {
                if (Current() == '\n')
                    throw new Error("Unterminated string", _currentPosition);

                if (Current() == '\\')
                {
                    Position escapePos = _currentPosition;

                    Next();

                    if (!NotAtEnd())
                        throw new Error("Unterminated escape sequence", escapePos);

                    char escapeCharacter = Current() switch
                    {
                        '\\' => '\\',
                        '"' => '\"',
                        't' => '\t',
                        'n' => '\n',
                        'r' => '\r',
                        'f' => '\f',
                        'a' => '\a',
                        'b' => '\b',
                        'e' => '\e',
                        'v' => '\v',
                        '0' => '\0',
                        _ => throw new Error($"'\\{Current()}' is an invalid escape character", escapePos)
                    };

                    Next();

                    sb.Append(escapeCharacter);
                    continue;
                }

                sb.Append(Current());

                Next();
            }

            if (!NotAtEnd())
                throw new Error("Unterminated string", _currentPosition);

            Next();

            return new Token(sb.ToString(), TokenType.String, pos);
        }

        Token LexAlpha()
        {
            Position pos = _currentPosition;

            StringBuilder sb = new StringBuilder();

            while (NotAtEnd() && char.IsAsciiLetterOrDigit(Current()))
            {
                sb.Append(Current());
                Next();
            }

            string identifier = sb.ToString();

            if (Keywords.TryGetValue(identifier, out TokenType type))
            {
                return new Token(identifier, type, pos);
            }

            return new Token(identifier, TokenType.Identifier, pos);
        }

        Token LexDigit()
        {
            Position pos = _currentPosition;

            StringBuilder sb = new StringBuilder();
            bool hasDecimal = false;

            while (NotAtEnd() && (char.IsDigit(Current()) || Current() == '.'))
            {
                if (Current() == '.')
                {
                    if (hasDecimal)
                        throw new Error("Duplicate decimal", _currentPosition);

                    if (!NotAtEnd(1) || !char.IsDigit(Current(1)))
                        throw new Error("Expected digit after decimal", _currentPosition);

                    hasDecimal = true;
                }

                sb.Append(Current());
                Next();
            }

            if (hasDecimal)
                return new Token(sb.ToString(), TokenType.Float, pos);

            return new Token(sb.ToString(), TokenType.Int, pos);
        }

        bool NotAtEnd(int dst = 0) => _i + dst < _code.Length;
        char Current(int dst = 0) => _code[_i + dst];

        void Next()
        {
            if (Current() == '\n')
            {
                _currentPosition.Line++;
                _currentPosition.Column = 1;
            }
            else
                _currentPosition.Column++;
            _i++;
        }
    }
}
