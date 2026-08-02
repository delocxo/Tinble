using System;
using System.Collections.Generic;
using System.Text;

namespace Tinble
{
    internal abstract record Expr(Position Position);
    internal record IntExpr(long Value, Position Position) : Expr(Position);
    internal record FloatExpr(double Value, Position Position) : Expr(Position);
    internal record StringExpr(string Value, Position Position) : Expr(Position);
    internal record BoolExpr(bool Value, Position Position) : Expr(Position);
    internal record NullExpr(Position Position) : Expr(Position);
    internal record NameExpr(string Name, Position Position) : Expr(Position);
    internal record UnaryExpr(Expr Right, TokenType Op, Position Position) : Expr(Position);
    internal record CallExpr(Expr Callee, List<Expr> Arguments, Position Position) : Expr(Position);
    internal record ArrayExpr(List<Expr> Elements, Position Position) : Expr(Position);
    internal record IndexGetExpr(Expr Target, Expr Index, Position Position) : Expr(Position);
    internal record MemberGetExpr(Expr Target, string Name, Position Position) : Expr(Position);
    internal record DictExpr(List<KeyValuePair<Expr, Expr>> KeyValuePairs, Position Position) : Expr(Position);
    internal record FuncExpr(List<string> Args, List<Stmt> Body, Position Position) : Expr(Position);
    internal record BinaryExpr(Expr Left, Expr Right, TokenType Op, Position Position) : Expr(Position);

    internal abstract record Stmt(Position Position);
    internal record VarStmt(string Name, Expr Expr, Position Position) : Stmt(Position);
    internal record VarReassignStmt(string Name, Expr Expr, Position Position) : Stmt(Position);
    internal record ConditionStmt(Expr Condition, List<Stmt> Body, Position Position) : Stmt(Position);
    internal record IfStmt(ConditionStmt If, List<ConditionStmt> ElseIfs, List<Stmt>? ElseBody, Position Position) : Stmt(Position);
    internal record WhileStmt(ConditionStmt While, Position Position) : Stmt(Position);
    internal record BreakStmt(Expr? Condition, Position Position) : Stmt(Position);
    internal record ContinueStmt(Expr? Condition, Position Position) : Stmt(Position);
    internal record FuncStmt(string Name, List<string> Args, List<Stmt> Body, Position Position) : Stmt(Position);
    internal record ReturnStmt(Expr Value, Expr? Condition, Position Position) : Stmt(Position);
    internal record CallStmt(CallExpr CallExpr, Expr? Condition, CallExpr? ElseCall, Position Position) : Stmt(Position);
    internal record IndexSetStmt(IndexGetExpr IndexGetExpr, Expr Value, Position Position) : Stmt(Position);
    internal record ForStmt(string? IndexName, string ValueName, Expr Target, List<Stmt> Body, Position Position) : Stmt(Position);
    internal record StructStmt(string Name, List<string> Members, Position Position) : Stmt(Position);
    internal record MemberSetStmt(MemberGetExpr MemberGetExpr, Expr Value, Position Position) : Stmt(Position);
    internal record ImportStmt(string FilePath, Position Position) : Stmt(Position);
    internal record EnumStmt(string Name, List<string> Enums, Position Position) : Stmt(Position);
}
