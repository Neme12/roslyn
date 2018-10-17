﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.CodeRefactorings
Imports Microsoft.CodeAnalysis.VisualBasic.SplitIntoNestedIfStatements

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.SplitIntoNestedIfStatements
    <Trait(Traits.Feature, Traits.Features.CodeActionsMergeNestedIfStatements)>
    Public NotInheritable Class MergeNestedIfStatementsTests
        Inherits AbstractVisualBasicCodeActionTest

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As Workspace, parameters As TestParameters) As CodeRefactoringProvider
            Return New VisualBasicMergeNestedIfStatementsCodeRefactoringProvider()
        End Function

        <Fact>
        Public Async Function MergedOnNestedIfCaret1() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedOnNestedIfCaret2() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            i[||]f b then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedOnNestedIfCaret3() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            if[||] b then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedOnNestedIfCaret4() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            if b [||]then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedOnNestedIfCaret5() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            if b then[||]
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedOnNestedIfSelection() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [|if|] b then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedOnNestedIfPartialSelection() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [|i|]f b then
            end if
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedOnNestedIfOverreachingSelection() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [|if |]b then
            end if
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedOnNestedIfConditionCaret() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            if [||]b then
            end if
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedOnOuterIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        [||]if a then
            if b then
            end if
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedOnSingleIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        [||]if b then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithAndAlsoExpressions() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if a andalso b then
            [||]if c andalso d then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if a andalso b AndAlso c andalso d then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithOrElseExpressionParenthesized1() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if a orelse b then
            [||]if c andalso d then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if (a orelse b) AndAlso c andalso d then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithOrElseExpressionParenthesized2() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if a andalso b then
            [||]if c orelse d then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if a andalso b AndAlso (c orelse d) then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithMixedExpressions1() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if a orelse b andalso c then
            [||]if c = d then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if (a orelse b andalso c) AndAlso c = d then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithMixedExpressions2() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if a = b then
            [||]if b andalso c orelse d then
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean, c as boolean, d as boolean)
        if a = b AndAlso (b andalso c orelse d) then
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithNestedIfInsideWhileLoop() As Task
            ' Do not consider the while loop to be a simple block (as might be suggested by some language-agnostic helpers).
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            while true
                [||]if b then
                end if
            end while
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithNestedIfInsideUsingStatement() As Task
            ' Do not consider the using statement to be a simple block (as might be suggested by some language-agnostic helpers).
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            using nothing
                [||]if b then
                end if
            end using
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithStatements() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a)
                System.Console.WriteLine(b)
            end if
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
            System.Console.WriteLine(a)
            System.Console.WriteLine(b)
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseClauseOnNestedIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine()
            end if
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseIfClauseOnNestedIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else if a then
                System.Console.WriteLine(a)
            end if
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseIfElseClausesOnNestedIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else if a then
                System.Console.WriteLine(a)
            else
                System.Console.WriteLine()
            end if
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseClauseOnOuterIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            end if
        else
            System.Console.WriteLine()
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseIfClauseOnOuterIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            end if
        else if a then
            System.Console.WriteLine(a)
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseIfElseClausesOnOuterIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            end if
        else if a then
            System.Console.WriteLine(a)
        else
            System.Console.WriteLine()
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseIfElseClauses1() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else if a then
                System.Console.WriteLine()
            end if
        else
            System.Console.WriteLine()
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseIfElseClauses2() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine()
            end if
        else if a then
            System.Console.WriteLine()
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseIfClauses1() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else if a then
                System.Console.WriteLine()
            end if
        else if b then
            System.Console.WriteLine()
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseIfClauses2() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else if a then
                System.Console.WriteLine(a)
            end if
        else if a then
            System.Console.WriteLine(b)
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithUnmatchingElseClauses() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if
        else
            System.Console.WriteLine(b)
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithMatchingElseClauses() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if
        else
            System.Console.WriteLine(a)
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
            System.Console.WriteLine(a andalso b)
        else
            System.Console.WriteLine(a)
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithMatchingElseIfClauses() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else if a then
                System.Console.WriteLine(a)
            end if
        else if a then
            System.Console.WriteLine(a)
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
            System.Console.WriteLine(a andalso b)
        else if a then
            System.Console.WriteLine(a)
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithMatchingElseIfElseClauses() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else if a then
                System.Console.WriteLine(a)
            else
                System.Console.WriteLine()
            end if
        else if a then
            System.Console.WriteLine(a)
        else
            System.Console.WriteLine()
        end if
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
            System.Console.WriteLine(a andalso b)
        else if a then
            System.Console.WriteLine(a)
        else
            System.Console.WriteLine()
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithExtraUnmatchingStatementBelowNestedIf() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            System.Console.WriteLine(b)
        else
            System.Console.WriteLine(a)
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithExtraUnmatchingStatementBelowOuterIf() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if
        else
            System.Console.WriteLine(a)
        end if

        System.Console.WriteLine(b)
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
            System.Console.WriteLine(a andalso b)
        else
            System.Console.WriteLine(a)
        end if

        System.Console.WriteLine(b)
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithExtraUnmatchingStatementsIfControlFlowContinues() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            System.Console.WriteLine(a)
            System.Console.WriteLine(b)
        else
            System.Console.WriteLine(a)
        end if

        System.Console.WriteLine(b)
        System.Console.WriteLine(a)
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithExtraUnmatchingStatementsIfControlFlowQuits() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            throw new System.Exception()
        else
            System.Console.WriteLine(a)
        end if

        return
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithExtraPrecedingMatchingStatementsIfControlFlowQuits() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        return

        if a then
            return

            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if
        else
            System.Console.WriteLine(a)
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithExtraMatchingStatementsIfControlFlowContinues1() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            System.Console.WriteLine(a)
            System.Console.WriteLine(b)
        else
            System.Console.WriteLine(a)
        end if

        System.Console.WriteLine(a)
        System.Console.WriteLine(b)
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithExtraMatchingStatementsIfControlFlowContinues2() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            if a then
                return
            end if
        else
            System.Console.WriteLine(a)
        end if

        if a then
            return
        end if
    end sub
end class")
        End Function

        <Fact>
        Public Async Function NotMergedWithExtraMatchingStatementsIfControlFlowContinues3() As Task
            Await TestMissingInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        while a <> b
            if a then
                [||]if b then
                    System.Console.WriteLine(a andalso b)
                else
                    System.Console.WriteLine(a)
                end if

                while a <> b
                    continue while
                end while
            else
                System.Console.WriteLine(a)
            end if

            while a <> b
                continue while
            end while
        end while
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithExtraMatchingStatementsIfControlFlowQuits1() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            return
        else
            System.Console.WriteLine(a)
        end if

        return
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
            System.Console.WriteLine(a andalso b)
        else
            System.Console.WriteLine(a)
        end if

        return
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithExtraMatchingStatementsIfControlFlowQuits2() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        if a then
            [||]if b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            System.Console.WriteLine(a)
            throw new System.Exception()
        else
            System.Console.WriteLine(a)
        end if

        System.Console.WriteLine(a)
        throw new System.Exception()
        System.Console.WriteLine(b)
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        if a AndAlso b then
            System.Console.WriteLine(a andalso b)
        else
            System.Console.WriteLine(a)
        end if

        System.Console.WriteLine(a)
        throw new System.Exception()
        System.Console.WriteLine(b)
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithExtraMatchingStatementsIfControlFlowQuits3() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        while a <> b
            if a then
                [||]if b then
                    System.Console.WriteLine(a andalso b)
                else
                    System.Console.WriteLine(a)
                end if

                continue while
            else
                System.Console.WriteLine(a)
            end if

            continue while
        end while
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        while a <> b
            if a AndAlso b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            continue while
        end while
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithExtraMatchingStatementsIfControlFlowQuits4() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        while a <> b
            System.Console.WriteLine()

            if a then
                [||]if b then
                    System.Console.WriteLine(a andalso b)
                else
                    System.Console.WriteLine(a)
                end if

                if a then
                    continue while
                else
                    exit while
                end if
            else
                System.Console.WriteLine(a)
            end if

            if a then
                continue while
            else
                exit while
            end if
        end while
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        while a <> b
            System.Console.WriteLine()

            if a AndAlso b then
                System.Console.WriteLine(a andalso b)
            else
                System.Console.WriteLine(a)
            end if

            if a then
                continue while
            else
                exit while
            end if
        end while
    end sub
end class")
        End Function

        <Fact>
        Public Async Function MergedWithExtraMatchingStatementsIfControlFlowQuitsInCaseBlock() As Task
            Await TestInRegularAndScriptAsync(
"class C
    sub M(a as boolean, b as boolean)
        select a
            case else
                System.Console.WriteLine()

                if a then
                    [||]if b then
                        System.Console.WriteLine(a andalso b)
                    end if

                    exit select
                end if

                exit select
        end select
    end sub
end class",
"class C
    sub M(a as boolean, b as boolean)
        select a
            case else
                System.Console.WriteLine()

                if a AndAlso b then
                    System.Console.WriteLine(a andalso b)
                end if

                exit select
        end select
    end sub
end class")
        End Function
    End Class
End Namespace
