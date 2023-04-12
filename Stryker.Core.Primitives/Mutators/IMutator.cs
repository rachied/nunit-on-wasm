using Microsoft.CodeAnalysis;
using Stryker.Core.Primitives.Mutants;
using Stryker.Core.Primitives.Options;
using System.Collections.Generic;

namespace Stryker.Core.Primitives.Mutators
{
    public interface IMutator
    {
        IEnumerable<Mutation> Mutate(SyntaxNode node, StrykerOptions options);
    }
}
