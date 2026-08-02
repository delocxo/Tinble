using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

namespace Tinble
{
    internal class Parser
    {
        List<Token> _tokens;
        int _i = 0;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }

        public List<Stmt> Parse()
        {
            List<Stmt> stmts = new List<Stmt>();

            while (NotAtEnd())
                stmts.Add(ParseStmt());

            return stmts;
        }

        Stmt ParseStmt()
        {
            if (Check(TokenType.Make))
                return ParseMake();
            else if (Check(TokenType.Identifier))
                return ParseIdentifier();
            else if (Check(TokenType.If))
                return ParseIf();
            else if (Check(TokenType.While))
                return ParseWhile();
            else if (Check(TokenType.Break))
                return ParseBreak();
            else if (Check(TokenType.Continue))
                return ParseContinue();
            else if (Check(TokenType.Return))
                return ParseReturn();
            else if (Check(TokenType.For))
                return ParseFor();
            else if (Check(TokenType.Import))
                return ParseImport();
            throw ThrowUnexpected();
        }

        Stmt ParseMake()
        {
            Position position = Current().Position;

            Next();

            string name = ParseName();

            if (Match(TokenType.Equal))
            {
                Expr expr = ParseExpr();

                Expect(TokenType.Semicolon);

                return new VarStmt(name, expr, position);
            }
            else if (Check(TokenType.LeftParen))
            {
                List<string> arguments = ParseFuncArgs(TokenType.LeftParen, TokenType.RightParen);

                List<Stmt> body = ParseBody();

                return new FuncStmt(name, arguments, body, position);
            }
            else if (Check(TokenType.LeftBrace))
            {
                List<string> members = ParseFuncArgs(TokenType.LeftBrace, TokenType.RightBrace);

                return new StructStmt(name, members, position);
            }
            else if (Match(TokenType.Enum))
            {
                List<string> enums = ParseFuncArgs(TokenType.LeftBrace, TokenType.RightBrace);

                return new EnumStmt(name, enums, position);
            }
                
            throw new Error("Invalid make statement", position);
        }

        Stmt ParseIdentifier()
        {
            Position position = Current().Position;

            Expr assignee = ParsePostfix();

            if (assignee is CallExpr callExpr)
            {
                Expr? condition = null;
                CallExpr? elseCall = null;
                if (Match(TokenType.If))
                {
                    condition = ParseExpr();
                    if (Match(TokenType.Else))
                    {
                        Expr elseCallExpr = ParsePostfix();
                        if (elseCallExpr is not CallExpr other)
                            throw new Error("Expected an else call", position);
                        elseCall = other;
                    }
                }
                Expect(TokenType.Semicolon);
                return new CallStmt(callExpr, condition, elseCall, position);
            }

            Expect(TokenType.Equal);

            Expr expr = ParseExpr();

            Expect(TokenType.Semicolon);

            if (assignee is NameExpr nameExpr)
                return new VarReassignStmt(nameExpr.Name, expr, position);
            else if (assignee is IndexGetExpr indexGetExpr)
                return new IndexSetStmt(indexGetExpr, expr, position);
            else if (assignee is MemberGetExpr memberGetExpr)
                return new MemberSetStmt(memberGetExpr, expr, position);

            throw new Error("Invalid reassign target", position);
        }

        IfStmt ParseIf()
        {
            Position position = Current().Position;

            Next();

            ConditionStmt ifCondition = ParseCondition();

            List<ConditionStmt> elseifs = new List<ConditionStmt>();

            while (Match(TokenType.Elseif))
                elseifs.Add(ParseCondition());

            if (Match(TokenType.Else))
            {
                List<Stmt> elseBody = ParseBody();
                return new IfStmt(ifCondition, elseifs, elseBody, position);
            }

            return new IfStmt(ifCondition, elseifs, null, position);
        }

        WhileStmt ParseWhile()
        {
            Position position = Current().Position;

            Next();

            ConditionStmt whileCondition = ParseCondition();

            return new WhileStmt(whileCondition, position);
        }

        BreakStmt ParseBreak()
        {
            Position position = Current().Position;

            Next();

            Expr? condition = null;

            if (Match(TokenType.If))
            {
                condition = ParseExpr();
            }

            Expect(TokenType.Semicolon);

            return new BreakStmt(condition, position);
        }

        ContinueStmt ParseContinue()
        {
            Position position = Current().Position;

            Next();

            Expr? condition = null;

            if (Match(TokenType.If))
            {
                condition = ParseExpr();
            }

            Expect(TokenType.Semicolon);

            return new ContinueStmt(condition, position);
        }

        ReturnStmt ParseReturn()
        {
            Position position = Current().Position;

            Next();

            Expr value = ParseExpr();
            Expr? condition = null;

            if (Match(TokenType.If))
                condition = ParseExpr();

            Expect(TokenType.Semicolon);

            return new ReturnStmt(value, condition, position);
        }

        ForStmt ParseFor()
        {
            Position position = Current().Position;

            Next();

            string value = ParseName();
            string? iterater = null;

            if (Match(TokenType.Comma))
                iterater = ParseName();

            Expect(TokenType.In);

            Expr expr = ParseExpr();

            List<Stmt> body = ParseBody();

            return new ForStmt(iterater, value, expr, body, position);
        }

        ImportStmt ParseImport()
        {
            Position position = Current().Position;

            Next();

            string importPath = Current().Lexeme;
            Eat("Expected a import path string", TokenType.String);

            Expect(TokenType.Semicolon);

            return new ImportStmt(importPath, position);
        }

        Error ThrowUnexpected()
        {
            Token token = Current();
            string? keyword = Lexer.GetKeywordFromType(token.TokenType);
            string? symbol = Lexer.GetSymbolFromType(token.TokenType);
            if (keyword != null)
                throw new Error($"Unexpected keyword '{keyword}'", token.Position);
            else if (symbol != null)
                throw new Error($"Unexpected symbol '{symbol}'", token.Position);
            else
                throw new Error($"Unexpected token '{token.TokenType}'", token.Position);
        }

        string ParseName()
        {
            string name = Current().Lexeme;
            Eat("Expected name", TokenType.Identifier);
            return name;
        }

        List<string> ParseFuncArgs(TokenType front, TokenType end)
        {
            Expect(front);

            List<string> args = new List<string>();

            if (Match(end))
                return args;

            args.Add(ParseName());

            while (Match(TokenType.Comma))
                args.Add(ParseName());

            Expect(end);

            return args;
        }

        List<Expr> ParseCallArgs(TokenType left, TokenType right)
        {
            Expect(left);

            List<Expr> args = new List<Expr>();

            if (Match(right))
                return args;

            args.Add(ParseExpr());

            while (Match(TokenType.Comma))
                args.Add(ParseExpr());

            Expect(right);

            return args;
        }

        ConditionStmt ParseCondition()
        {
            Position position = Current().Position;
            Expr condition = ParseExpr();
            List<Stmt> body = ParseBody();
            return new ConditionStmt(condition, body, position);
        }

        List<Stmt> ParseBody()
        {
            List<Stmt> stmts = new List<Stmt>();

            if (!Check(TokenType.LeftBrace))
            {
                stmts.Add(ParseStmt());
                return stmts;
            }    

            Expect(TokenType.LeftBrace);

            while (NotAtEnd() && !Check(TokenType.RightBrace))
                stmts.Add(ParseStmt());

            Expect(TokenType.RightBrace);

            return stmts;
        }

        bool Check(params TokenType[] types)
        {
            for (int i = 0; i < types.Length; i++)
                if (Current().TokenType == types[i])
                    return true;
            return false;
        }

        Token Current() => _tokens[_i];
        bool NotAtEnd() => !Check(TokenType.EOF);
        bool AtEnd() => Check(TokenType.EOF);
        void Next() => _i++;
 
        void Eat(string message, params TokenType[] types)
        {
            if (Check(types))
            {
                Next();
                return;
            }
            throw new Error(message, Current().Position);
        }

        bool Match(params TokenType[] types)
        {
            if (Check(types))
            {
                Next();
                return true;
            }
            return false;
        }

        void Expect(TokenType type)
        {
            if (Check(type))
            {
                Next();
                return;
            }
            string? keyword = Lexer.GetKeywordFromType(type);
            string? symbol = Lexer.GetSymbolFromType(type);
            if (keyword != null)
                throw new Error($"Expected keyword '{keyword}'", Current().Position);
            else if (symbol != null)
                throw new Error($"Expected symbol '{symbol}'", Current().Position);
            else
                throw new Error($"Expected token '{type}'", Current().Position);
        }

        DictExpr ParseDict()
        {
            KeyValuePair<Expr, Expr> ParseKeyValuePair()
            {
                Expr key = ParseExpr();
                Expect(TokenType.Colon);
                Expr value = ParseExpr();
                return new KeyValuePair<Expr, Expr>(key, value);
            }

            Position position = Current().Position;

            Next();

            Expect(TokenType.LeftBrace);

            List<KeyValuePair<Expr, Expr>> keyValuePairs = new List<KeyValuePair<Expr, Expr>>();

            if (Match(TokenType.RightBrace))
                return new DictExpr(keyValuePairs, position);

            keyValuePairs.Add(ParseKeyValuePair());

            while (Match(TokenType.Comma))
                keyValuePairs.Add(ParseKeyValuePair());

            Expect(TokenType.RightBrace);

            return new DictExpr(keyValuePairs, position);
        }

        Expr ParsePrimary()
        {
            Token token = Current();

            if (Match(TokenType.Int))
                if (long.TryParse(token.Lexeme, out long result))
                    return new IntExpr(result, token.Position);
                else
                    throw new Error("Integer out of range", token.Position);
            else if (Match(TokenType.Float))
                return new FloatExpr(double.Parse(token.Lexeme), token.Position);
            else if (Match(TokenType.String))
                return new StringExpr(token.Lexeme, token.Position);
            else if (Match(TokenType.True))
                return new BoolExpr(true, token.Position);
            else if (Match(TokenType.False))
                return new BoolExpr(false, token.Position);
            else if (Match(TokenType.Null))
                return new NullExpr(token.Position);
            else if (Match(TokenType.Identifier))
                return new NameExpr(token.Lexeme, token.Position);
            else if (Match(TokenType.LeftParen))
            {
                Expr expr = ParseExpr();
                Expect(TokenType.RightParen);
                return expr;
            }
            else if (Check(TokenType.LeftBracket))
            {
                List<Expr> exprs = ParseCallArgs(TokenType.LeftBracket, TokenType.RightBracket);
                return new ArrayExpr(exprs, token.Position);
            }
            else if (Check(TokenType.Dict))
            {
                if (_i + 1 < _tokens.Count && _tokens[_i + 1].TokenType == TokenType.LeftBrace)
                    return ParseDict();
                Next();
                return new NameExpr("dict", token.Position);
            }
            else if (Match(TokenType.Make))
            {
                if (Check(TokenType.LeftParen))
                {
                    List<string> args = ParseFuncArgs(TokenType.LeftParen, TokenType.RightParen);
                    List<Stmt> body = ParseBody();
                    return new FuncExpr(args, body, token.Position);
                }
                throw new Error("Invalid use of make", token.Position);
            }

            throw new Error("Invalid expression", token.Position);
        }

        Expr ParsePostfix()
        {
            Expr left = ParsePrimary();

            while (Check(TokenType.LeftParen, TokenType.LeftBracket, TokenType.Dot))
            {
                if (Check(TokenType.LeftParen))
                {
                    Position position = Current().Position;
                    List<Expr> args = ParseCallArgs(TokenType.LeftParen, TokenType.RightParen);
                    left = new CallExpr(left, args, position);
                    continue;
                }

                if (Check(TokenType.LeftBracket))
                {
                    Position position = Current().Position;

                    Next();

                    Expr index = ParseExpr();

                    Expect(TokenType.RightBracket);

                    left = new IndexGetExpr(left, index, position);
                    continue;
                }

                if (Check(TokenType.Dot))
                {
                    Position position = Current().Position;

                    Next();

                    string name = ParseName();

                    left = new MemberGetExpr(left, name, position);
                    continue;
                }

                break;
            }
            return left;
        }

        Expr ParseUnary()
        {
            if (Check(TokenType.Sub, TokenType.Bang))
            {
                Token op = Current();

                Next();

                Expr right = ParseUnary();

                return new UnaryExpr(right, op.TokenType, op.Position);
            }
            return ParsePostfix();
        }

        Expr ParseTerm()
        {
            Expr left = ParseUnary();

            while (Check(TokenType.Mul, TokenType.Div, TokenType.Mod))
            {
                Token op = Current();

                Next();

                Expr right = ParseUnary();

                left = new BinaryExpr(left, right, op.TokenType, op.Position);
            }

            return left;
        }

        Expr ParseFactor()
        {
            Expr left = ParseTerm();

            while (Check(TokenType.Add, TokenType.Sub))
            {
                Token op = Current();

                Next();

                Expr right = ParseTerm();

                left = new BinaryExpr(left, right, op.TokenType, op.Position);
            }

            return left;
        }

        Expr ParseComparison()
        {
            Expr left = ParseFactor();

            while (Check(TokenType.Less, TokenType.Greater, TokenType.LessEq, TokenType.GreaterEq))
            {
                Token op = Current();

                Next();

                Expr right = ParseFactor();

                left = new BinaryExpr(left, right, op.TokenType, op.Position);
            }

            return left;
        }

        Expr ParseEquality()
        {
            Expr left = ParseComparison();

            while (Check(TokenType.Equals, TokenType.NotEquals))
            {
                Token op = Current();

                Next();

                Expr right = ParseComparison();

                left = new BinaryExpr(left, right, op.TokenType, op.Position);
            }

            return left;
        }

        Expr ParseAnd()
        {
            Expr left = ParseEquality();

            while (Check(TokenType.And))
            {
                Token op = Current();

                Next();

                Expr right = ParseEquality();

                left = new BinaryExpr(left, right, op.TokenType, op.Position);
            }

            return left;
        }

        Expr ParseOr()
        {
            Expr left = ParseAnd();

            while (Check(TokenType.Or))
            {
                Token op = Current();

                Next();

                Expr right = ParseAnd();

                left = new BinaryExpr(left, right, op.TokenType, op.Position);
            }

            return left;
        }

        Expr ParseExpr() => ParseOr();
    }
}
