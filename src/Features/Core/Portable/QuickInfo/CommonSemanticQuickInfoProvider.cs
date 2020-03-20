﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.DocumentationComments;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Tags;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.QuickInfo
{
    internal abstract partial class CommonSemanticQuickInfoProvider : CommonQuickInfoProvider
    {
        private const int MaxConstantLength = 42;

        protected override async Task<QuickInfoItem?> BuildQuickInfoAsync(
            Document document,
            SyntaxToken token,
            CancellationToken cancellationToken)
        {
            var (model, symbols, supportedPlatforms) = await ComputeQuickInfoDataAsync(document, token, cancellationToken).ConfigureAwait(false);

            if (symbols.IsDefault)
            {
                return null;
            }

            if (model.SyntaxTree != token.SyntaxTree)
            {
                var linkedDocument = document.Project.Solution.GetRequiredDocument(model.SyntaxTree);
                token = await FindTokenInLinkedDocumentAsync(token, document, linkedDocument, cancellationToken).ConfigureAwait(false);
            }

            return await CreateContentAsync(document.Project.Solution.Workspace,
                token, model, symbols, supportedPlatforms,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<(SemanticModel model, ImmutableArray<ISymbol> symbols, SupportedPlatformData? supportedPlatforms)> ComputeQuickInfoDataAsync(
            Document document,
            SyntaxToken token,
            CancellationToken cancellationToken)
        {
            var linkedDocumentIds = document.GetLinkedDocumentIds();
            if (linkedDocumentIds.Any())
            {
                return await ComputeFromLinkedDocumentsAsync(document, linkedDocumentIds, token, cancellationToken).ConfigureAwait(false);
            }

            var (model, symbols) = await BindTokenAsync(document, token, cancellationToken).ConfigureAwait(false);

            return (model, symbols, supportedPlatforms: null);
        }

        private async Task<(SemanticModel model, ImmutableArray<ISymbol> symbols, SupportedPlatformData supportedPlatforms)> ComputeFromLinkedDocumentsAsync(
            Document document,
            ImmutableArray<DocumentId> linkedDocumentIds,
            SyntaxToken token,
            CancellationToken cancellationToken)
        {
            // Linked files/shared projects: imagine the following when GOO is false
            // #if GOO
            // int x = 3;
            // #endif
            // var y = x$$;
            //
            // 'x' will bind as an error type, so we'll show incorrect information.
            // Instead, we need to find the head in which we get the best binding,
            // which in this case is the one with no errors.

            var (model, symbols) = await BindTokenAsync(document, token, cancellationToken).ConfigureAwait(false);

            var candidateProjects = new List<ProjectId>() { document.Project.Id };
            var invalidProjects = new List<ProjectId>();

            var candidateResults = new List<(DocumentId docId, SemanticModel model, ImmutableArray<ISymbol> symbols)>
            {
                (document.Id, model, symbols)
            };

            foreach (var linkedDocumentId in linkedDocumentIds)
            {
                var linkedDocument = document.Project.Solution.GetRequiredDocument(linkedDocumentId);
                var linkedToken = await FindTokenInLinkedDocumentAsync(token, document, linkedDocument, cancellationToken).ConfigureAwait(false);

                if (linkedToken != default)
                {
                    // Not in an inactive region, so this file is a candidate.
                    candidateProjects.Add(linkedDocumentId.ProjectId);
                    var (linkedModel, linkedSymbols) = await BindTokenAsync(linkedDocument, linkedToken, cancellationToken).ConfigureAwait(false);
                    candidateResults.Add((linkedDocumentId, linkedModel, linkedSymbols));
                }
            }

            // Take the first result with no errors.
            // If every file binds with errors, take the first candidate, which is from the current file.
            var bestBinding = candidateResults.FirstOrNull(c => HasNoErrors(c.symbols))
                ?? candidateResults.First();

            if (bestBinding.symbols.IsDefaultOrEmpty)
            {
                return default;
            }

            // We calculate the set of supported projects
            candidateResults.Remove(bestBinding);
            foreach (var candidate in candidateResults)
            {
                // Does the candidate have anything remotely equivalent?
                if (!candidate.symbols.Intersect(bestBinding.symbols, LinkedFilesSymbolEquivalenceComparer.Instance).Any())
                {
                    invalidProjects.Add(candidate.docId.ProjectId);
                }
            }

            var supportedPlatforms = new SupportedPlatformData(invalidProjects, candidateProjects, document.Project.Solution.Workspace);

            return (bestBinding.model, bestBinding.symbols, supportedPlatforms);
        }

        private static bool HasNoErrors(ImmutableArray<ISymbol> symbols)
            => symbols.Length > 0
                && !ErrorVisitor.ContainsError(symbols.FirstOrDefault());

        private async Task<SyntaxToken> FindTokenInLinkedDocumentAsync(
            SyntaxToken token,
            Document originalDocument,
            Document linkedDocument,
            CancellationToken cancellationToken)
        {
            var root = await linkedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (root == null)
            {
                return default;
            }

            // Don't search trivia because we want to ignore inactive regions
            var linkedToken = root.FindToken(token.SpanStart);

            // The new and old tokens should have the same span?
            if (token.Span == linkedToken.Span)
            {
                return linkedToken;
            }

            return default;
        }

        protected async Task<QuickInfoItem?> CreateContentAsync(
            Workspace workspace,
            SyntaxToken token,
            SemanticModel semanticModel,
            IEnumerable<ISymbol> symbols,
            SupportedPlatformData? supportedPlatforms,
            CancellationToken cancellationToken)
        {
            var languageServices = workspace.Services.GetLanguageServices(semanticModel.Language);
            var descriptionService = languageServices.GetRequiredService<ISymbolDisplayService>();
            var formatter = languageServices.GetRequiredService<IDocumentationCommentFormattingService>();
            var syntaxFactsService = languageServices.GetRequiredService<ISyntaxFactsService>();
            var syntaxKindsService = languageServices.GetRequiredService<ISyntaxKindsService>();
            var showWarningGlyph = supportedPlatforms != null && supportedPlatforms.HasValidAndInvalidProjects();
            var showSymbolGlyph = true;

            var groups = await descriptionService.ToDescriptionGroupsAsync(workspace, semanticModel, token.SpanStart, symbols.AsImmutable(), cancellationToken).ConfigureAwait(false);

            bool TryGetGroupText(SymbolDescriptionGroups group, out ImmutableArray<TaggedText> taggedParts)
                => groups.TryGetValue(group, out taggedParts) && !taggedParts.IsDefaultOrEmpty;

            var sections = ImmutableArray.CreateBuilder<QuickInfoSection>(initialCapacity: groups.Count);

            void AddSection(string kind, ImmutableArray<TaggedText> taggedParts)
            {
                if (sections.Count == 0 && taggedParts.FirstOrDefault().Tag == TextTags.LineBreak)
                    taggedParts = taggedParts.RemoveAt(0);

                sections.Add(QuickInfoSection.Create(kind, taggedParts));
            }

            if (TryGetGroupText(SymbolDescriptionGroups.MainDescription, out var mainDescriptionTaggedParts))
            {
                AddSection(QuickInfoSectionKinds.Description, mainDescriptionTaggedParts);
            }

            var documentedSymbol = symbols.FirstOrDefault()?.OriginalDefinition;

            // if generating quick info for an attribute, bind to the class instead of the constructor
            if (syntaxFactsService.IsAttributeName(token.Parent) &&
                documentedSymbol?.ContainingType?.IsAttribute() == true)
            {
                documentedSymbol = documentedSymbol.ContainingType;
            }

            var documentationContent = GetDocumentationContent(documentedSymbol, groups, semanticModel, token, formatter, cancellationToken);
            if (syntaxFactsService.IsAwaitKeyword(token) &&
                (symbols.FirstOrDefault() as INamedTypeSymbol)?.SpecialType == SpecialType.System_Void)
            {
                documentationContent = default;
                showSymbolGlyph = false;
            }

            if (!documentationContent.IsDefaultOrEmpty)
            {
                AddSection(QuickInfoSectionKinds.DocumentationComments, documentationContent);
            }

            var remarksDocumentationContent = GetRemarksDocumentationContent(documentedSymbol, groups, semanticModel, token, formatter, cancellationToken);
            if (!remarksDocumentationContent.IsDefaultOrEmpty)
            {
                var builder = ImmutableArray.CreateBuilder<TaggedText>();
                if (!documentationContent.IsDefaultOrEmpty)
                {
                    builder.AddLineBreak();
                }

                builder.AddRange(remarksDocumentationContent);
                AddSection(QuickInfoSectionKinds.RemarksDocumentationComments, builder.ToImmutable());
            }

            var returnsDocumentationContent = GetReturnsDocumentationContent(documentedSymbol, groups, semanticModel, token, formatter, cancellationToken);
            if (!returnsDocumentationContent.IsDefaultOrEmpty)
            {
                var builder = ImmutableArray.CreateBuilder<TaggedText>();
                builder.AddLineBreak();
                builder.AddText(FeaturesResources.Returns_colon);
                builder.AddLineBreak();
                builder.Add(new TaggedText(TextTags.ContainerStart, "  "));
                builder.AddRange(returnsDocumentationContent);
                builder.Add(new TaggedText(TextTags.ContainerEnd, string.Empty));
                AddSection(QuickInfoSectionKinds.ReturnsDocumentationComments, builder.ToImmutable());
            }

            var valueDocumentationContent = GetValueDocumentationContent(documentedSymbol, groups, semanticModel, token, formatter, cancellationToken);
            if (!valueDocumentationContent.IsDefaultOrEmpty)
            {
                var builder = ImmutableArray.CreateBuilder<TaggedText>();
                builder.AddLineBreak();
                builder.AddText(FeaturesResources.Value_colon);
                builder.AddLineBreak();
                builder.Add(new TaggedText(TextTags.ContainerStart, "  "));
                builder.AddRange(valueDocumentationContent);
                builder.Add(new TaggedText(TextTags.ContainerEnd, string.Empty));
                AddSection(QuickInfoSectionKinds.ValueDocumentationComments, builder.ToImmutable());
            }

            var constantValueContent = GetConstantValueContent(token, semanticModel, syntaxFactsService, syntaxKindsService, descriptionService, cancellationToken);
            if (!constantValueContent.IsDefaultOrEmpty)
            {
                AddSection(QuickInfoSectionKinds.ConstantValue, constantValueContent);
            }

            if (TryGetGroupText(SymbolDescriptionGroups.TypeParameterMap, out var typeParameterMapText))
            {
                var builder = ImmutableArray.CreateBuilder<TaggedText>();
                builder.AddLineBreak();
                builder.AddRange(typeParameterMapText);
                AddSection(QuickInfoSectionKinds.TypeParameters, builder.ToImmutable());
            }

            if (TryGetGroupText(SymbolDescriptionGroups.AnonymousTypes, out var anonymousTypesText))
            {
                var builder = ImmutableArray.CreateBuilder<TaggedText>();
                builder.AddLineBreak();
                builder.AddRange(anonymousTypesText);
                AddSection(QuickInfoSectionKinds.AnonymousTypes, builder.ToImmutable());
            }

            var usageTextBuilder = ImmutableArray.CreateBuilder<TaggedText>();
            if (TryGetGroupText(SymbolDescriptionGroups.AwaitableUsageText, out var awaitableUsageText))
            {
                usageTextBuilder.AddRange(awaitableUsageText);
            }

            var nullableAnalysis = TryGetNullabilityAnalysis(workspace, semanticModel, token, cancellationToken);
            if (!nullableAnalysis.IsDefaultOrEmpty)
            {
                AddSection(QuickInfoSectionKinds.NullabilityAnalysis, nullableAnalysis);
            }

            if (supportedPlatforms != null)
            {
                usageTextBuilder.AddRange(supportedPlatforms.ToDisplayParts().ToTaggedText());
            }

            if (usageTextBuilder.Count > 0)
            {
                AddSection(QuickInfoSectionKinds.Usage, usageTextBuilder.ToImmutable());
            }

            if (TryGetGroupText(SymbolDescriptionGroups.Exceptions, out var exceptionsText))
            {
                AddSection(QuickInfoSectionKinds.Exception, exceptionsText);
            }

            if (TryGetGroupText(SymbolDescriptionGroups.Captures, out var capturesText))
            {
                AddSection(QuickInfoSectionKinds.Captures, capturesText);
            }

            var tags = ImmutableArray<string>.Empty;
            if (showSymbolGlyph && symbols.Any())
            {
                tags = tags.AddRange(GlyphTags.GetTags(symbols.First().GetGlyph()));
            }

            if (showWarningGlyph)
            {
                tags = tags.Add(WellKnownTags.Warning);
            }

            return sections.Count > 0 ? QuickInfoItem.Create(token.Span, tags, sections.ToImmutable()) : null;
        }

        private ImmutableArray<TaggedText> GetDocumentationContent(
            ISymbol? documentedSymbol,
            IDictionary<SymbolDescriptionGroups, ImmutableArray<TaggedText>> sections,
            SemanticModel semanticModel,
            SyntaxToken token,
            IDocumentationCommentFormattingService formatter,
            CancellationToken cancellationToken)
        {
            if (sections.TryGetValue(SymbolDescriptionGroups.Documentation, out var parts))
            {
                return parts;
            }
            else if (documentedSymbol is object)
            {
                var documentation = documentedSymbol.GetDocumentationParts(semanticModel, token.SpanStart, formatter, cancellationToken);
                if (documentation != null)
                {
                    return documentation.ToImmutableArray();
                }
            }

            return default;
        }

        private ImmutableArray<TaggedText> GetRemarksDocumentationContent(
            ISymbol? documentedSymbol,
            IDictionary<SymbolDescriptionGroups, ImmutableArray<TaggedText>> sections,
            SemanticModel semanticModel,
            SyntaxToken token,
            IDocumentationCommentFormattingService formatter,
            CancellationToken cancellationToken)
        {
            if (sections.TryGetValue(SymbolDescriptionGroups.RemarksDocumentation, out var parts))
            {
                return parts;
            }
            else if (documentedSymbol is object)
            {
                var documentation = documentedSymbol.GetRemarksDocumentationParts(semanticModel, token.SpanStart, formatter, cancellationToken);
                if (documentation != null)
                {
                    return documentation.ToImmutableArray();
                }
            }

            return default;
        }

        private static ImmutableArray<TaggedText> GetConstantValueContent(
            SyntaxToken token,
            SemanticModel semanticModel,
            ISyntaxFacts syntaxFacts,
            ISyntaxKinds syntaxKinds,
            ISymbolDisplayService displayService,
            CancellationToken cancellationToken)
        {
            if (token.RawKind == syntaxKinds.OpenParenToken || token.RawKind == syntaxKinds.CloseParenToken)
                return default;

            RoslynDebug.AssertNotNull(token.Parent);
            var constant = semanticModel.GetConstantValue(token.Parent, cancellationToken);
            if (!constant.HasValue)
                return default;

            var textBuilder = ImmutableArray.CreateBuilder<TaggedText>();
            textBuilder.AddLineBreak();
            textBuilder.AddText(FeaturesResources.Constant_value_colon);
            textBuilder.AddSpace();

            if (IsBinaryExpression(syntaxFacts, token.Parent, out var left, out var operatorToken, out var right) &&
                token == operatorToken &&
                semanticModel.GetConstantValue(left, cancellationToken) is var leftConstant && leftConstant.HasValue &&
                semanticModel.GetConstantValue(right, cancellationToken) is var rightConstant && rightConstant.HasValue)
            {
                textBuilder.AddRange(FormatValue(semanticModel.GetTypeInfo(left, cancellationToken).ConvertedType, leftConstant.Value, displayService, semanticModel, left.SpanStart));
                textBuilder.AddSpace();
                textBuilder.Add(
                    new TaggedText(
                        syntaxFacts.IsReservedKeyword(operatorToken) ? TextTags.Keyword : TextTags.Operator,
                        operatorToken.Text));
                textBuilder.AddSpace();
                textBuilder.AddRange(FormatValue(semanticModel.GetTypeInfo(right, cancellationToken).ConvertedType, rightConstant.Value, displayService, semanticModel, right.SpanStart));
                textBuilder.AddSpace();
                textBuilder.AddOperator("=");
                textBuilder.AddSpace();
            }

            textBuilder.AddRange(FormatValue(semanticModel.GetTypeInfo(token.Parent, cancellationToken).Type, constant.Value, displayService, semanticModel, token.SpanStart));

            return textBuilder.ToImmutable();

            // Local function
            static ImmutableArray<TaggedText> FormatValue(ITypeSymbol? type, object value, ISymbolDisplayService displayService, SemanticModel semanticModel, int position)
            {
                var taggedText = displayService.PrimitiveToMinimalDisplayParts(semanticModel, position, type, value, format: null).ToTaggedText();
                return TrimTaggedTextRun(taggedText, MaxConstantLength);
            }
        }

        private static bool IsBinaryExpression(
            ISyntaxFacts syntaxFacts,
            [NotNullWhen(true)] SyntaxNode? node,
            [NotNullWhen(true)] out SyntaxNode? left,
            out SyntaxToken operatorToken,
            [NotNullWhen(true)] out SyntaxNode? right)
        {
            if (syntaxFacts.IsBinaryExpression(node))
            {
                syntaxFacts.GetPartsOfBinaryExpression(node, out left, out operatorToken, out right);
                return true;
            }
            else
            {
                left = default;
                operatorToken = default;
                right = default;
                return false;
            }
        }

        private static ImmutableArray<TaggedText> TrimTaggedTextRun(ImmutableArray<TaggedText> taggedTextRun, int maxLength)
        {
            const string UnicodeEllipsis = "\u2026";

            for (int i = 0, length = 0; i < taggedTextRun.Length; ++i)
            {
                var tag = taggedTextRun[i].Tag;
                var text = taggedTextRun[i].Text;

                if (length + text.Length > maxLength)
                {
                    taggedTextRun = taggedTextRun.RemoveRange(i, taggedTextRun.Length - i);
                    taggedTextRun = taggedTextRun.Add(new TaggedText(tag, text.Substring(0, maxLength - length)));
                    taggedTextRun = taggedTextRun.Add(new TaggedText(TextTags.Text, UnicodeEllipsis));
                    break;
                }

                length = length + text.Length;
            }

            return taggedTextRun;
        }

        private ImmutableArray<TaggedText> GetReturnsDocumentationContent(
            ISymbol? documentedSymbol,
            IDictionary<SymbolDescriptionGroups, ImmutableArray<TaggedText>> sections,
            SemanticModel semanticModel,
            SyntaxToken token,
            IDocumentationCommentFormattingService formatter,
            CancellationToken cancellationToken)
        {
            if (sections.TryGetValue(SymbolDescriptionGroups.ReturnsDocumentation, out var parts))
            {
                return parts;
            }
            else if (documentedSymbol is object)
            {
                var documentation = documentedSymbol.GetReturnsDocumentationParts(semanticModel, token.SpanStart, formatter, cancellationToken);
                if (documentation != null)
                {
                    return documentation.ToImmutableArray();
                }
            }

            return default;
        }

        private ImmutableArray<TaggedText> GetValueDocumentationContent(
            ISymbol? documentedSymbol,
            IDictionary<SymbolDescriptionGroups, ImmutableArray<TaggedText>> sections,
            SemanticModel semanticModel,
            SyntaxToken token,
            IDocumentationCommentFormattingService formatter,
            CancellationToken cancellationToken)
        {
            if (sections.TryGetValue(SymbolDescriptionGroups.ValueDocumentation, out var parts))
            {
                return parts;
            }
            else if (documentedSymbol is object)
            {
                var documentation = documentedSymbol.GetValueDocumentationParts(semanticModel, token.SpanStart, formatter, cancellationToken);
                if (documentation != null)
                {
                    return documentation.ToImmutableArray();
                }
            }

            return default;
        }

        protected abstract bool GetBindableNodeForTokenIndicatingLambda(SyntaxToken token, [NotNullWhen(returnValue: true)] out SyntaxNode? found);
        protected abstract bool GetBindableNodeForTokenIndicatingPossibleIndexerAccess(SyntaxToken token, [NotNullWhen(returnValue: true)] out SyntaxNode? found);

        protected virtual ImmutableArray<TaggedText> TryGetNullabilityAnalysis(Workspace workspace, SemanticModel semanticModel, SyntaxToken token, CancellationToken cancellationToken) => default;

        private async Task<(SemanticModel semanticModel, ImmutableArray<ISymbol> symbols)> BindTokenAsync(
            Document document, SyntaxToken token, CancellationToken cancellationToken)
        {
            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var enclosingType = semanticModel.GetEnclosingNamedType(token.SpanStart, cancellationToken);

            var symbols = GetSymbolsFromToken(token, document.Project.Solution.Workspace, semanticModel, cancellationToken);

            var bindableParent = syntaxFacts.GetBindableParent(token);
            var overloads = semanticModel.GetMemberGroup(bindableParent, cancellationToken);

            symbols = symbols.Where(IsOk)
                             .Where(s => IsAccessible(s, enclosingType))
                             .Concat(overloads)
                             .Distinct(SymbolEquivalenceComparer.Instance)
                             .ToImmutableArray();

            if (symbols.Any())
            {
                var discardSymbols = (symbols.First() as ITypeParameterSymbol)?.TypeParameterKind == TypeParameterKind.Cref;
                return (semanticModel, discardSymbols ? ImmutableArray<ISymbol>.Empty : symbols);
            }

            // Couldn't bind the token to specific symbols.  If it's an operator, see if we can at
            // least bind it to a type.
            if (syntaxFacts.IsOperator(token))
            {
                var typeInfo = semanticModel.GetTypeInfo(token.Parent!, cancellationToken);
                if (IsOk(typeInfo.Type))
                {
                    return (semanticModel, ImmutableArray.Create<ISymbol>(typeInfo.Type));
                }
            }

            return (semanticModel, ImmutableArray<ISymbol>.Empty);
        }

        private ImmutableArray<ISymbol> GetSymbolsFromToken(SyntaxToken token, Workspace workspace, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (GetBindableNodeForTokenIndicatingLambda(token, out var lambdaSyntax))
            {
                var symbol = semanticModel.GetSymbolInfo(lambdaSyntax, cancellationToken).Symbol;
                return symbol != null ? ImmutableArray.Create(symbol) : ImmutableArray<ISymbol>.Empty;
            }

            if (GetBindableNodeForTokenIndicatingPossibleIndexerAccess(token, out var elementAccessExpression))
            {
                var symbol = semanticModel.GetSymbolInfo(elementAccessExpression, cancellationToken).Symbol;
                if (symbol?.IsIndexer() == true)
                {
                    return ImmutableArray.Create(symbol);
                }
            }

            return semanticModel.GetSemanticInfo(token, workspace, cancellationToken)
                .GetSymbols(includeType: true);
        }

        private static bool IsOk([NotNullWhen(returnValue: true)] ISymbol? symbol)
            => symbol != null && !symbol.IsErrorType();

        private static bool IsAccessible(ISymbol symbol, INamedTypeSymbol? within)
            => within == null
                || symbol.IsAccessibleWithin(within);
    }
}
