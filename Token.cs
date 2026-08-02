using System;
using System.Collections.Generic;
using System.Text;

namespace Tinble
{
    internal enum TokenType
    {
        String, Int, Float, True, False, Null,
        Identifier,

        Make, If, Else, Elseif, While, Break,
        Continue, Return, For, In, Import, Dict,

        Add, Sub, Mul, Div, Mod, Less, Greater,
        LessEq, GreaterEq, Equals, NotEquals,
        Bang, Equal, And, Or,

        LeftBrace, RightBrace, Semicolon, LeftParen,
        RightParen, Comma, LeftBracket, RightBracket,
        Dot, Colon,

        EOF
    }

    internal record Token
    {
        public Token(string lexeme, TokenType tokenType, Position position)
        {
            Lexeme = lexeme;
            TokenType = tokenType;
            Position = position;
        }

        public string Lexeme { get; }
        public TokenType TokenType { get; }
        public Position Position { get; }
    }
}
