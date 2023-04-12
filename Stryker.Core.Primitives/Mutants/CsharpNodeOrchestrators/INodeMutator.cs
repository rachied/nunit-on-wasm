using Microsoft.CodeAnalysis;
using Stryker.Core.Primitives.Helpers;

namespace Stryker.Core.Primitives.Mutants.CsharpNodeOrchestrators
{
    internal interface INodeMutator : ITypeHandler<SyntaxNode>
    {
        SyntaxNode Mutate(SyntaxNode node, MutationContext context);
    }
}
