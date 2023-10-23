﻿using Yabal.Exceptions;

namespace Yabal.Ast;

public record InitStructItem(Identifier? Name, Expression Value);

public record InitStructExpression(SourceRange Range, List<InitStructItem> Items, LanguageType? StructType) : Expression(Range)
{
    public override void Initialize(YabalBuilder builder)
    {
        foreach (var item in Items)
        {
            item.Value.Initialize(builder);
        }
    }

    public override void BuildExpressionToPointer(YabalBuilder builder, LanguageType suggestedType, Pointer pointer)
    {
        builder.InitStruct(suggestedType, pointer, this);
    }

    protected override void BuildExpressionCore(YabalBuilder builder, bool isVoid, LanguageType? suggestedType)
    {
        throw new InvalidCodeException("Cannot use struct initializer as an expression", Range);
    }

    public override bool OverwritesB => false;

    public override LanguageType Type => StructType ?? throw new InvalidOperationException();

    public override Expression CloneExpression()
    {
        return new InitStructExpression(
            Range,
            Items.Select(i => i with { Value = i.Value.CloneExpression() }).ToList(),
            StructType
        );
    }

    public override Expression Optimize(LanguageType? suggestedType)
    {
        return new InitStructExpression(
            Range,
            Items.Select(i => i with { Value = i.Value.Optimize(suggestedType) }).ToList(),
            StructType
        );
    }
}
