using TableStorage.Fluent;

namespace TableStorage;

public static class TableOptionsExtensions
{
    public static void EnableFluentCompilationAtRuntime(this TableOptions options)
    {
#if NET
        ArgumentNullException.ThrowIfNull(options);
#else
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }
#endif

        LazyExpressionCompilation.CompilationFactory = new FluentRuntimeCompilationFactory(LazyExpressionCompilation.CompilationFactory);
        TableSetExtensions.Visitor = new FluentMergeVisitorAndValidator();
    }
}