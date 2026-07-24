namespace LiteCqrs.Net.Tests.DuplicateFixture;

// Deliberately lives in its own tiny assembly, separate from LiteCqrs.Net.Tests: two handlers for
// the same closed ICommandHandler<,> pair must trip AssemblyScanner's duplicate-registration check
// whenever this assembly is scanned — keeping it isolated means it never interferes with the
// "successful scan" assertions that scan the main test assembly (which must stay duplicate-free).
public sealed record DuplicateHandledCommand : ICommand<string>;

public sealed class FirstDuplicateHandler : ICommandHandler<DuplicateHandledCommand, string>
{
    public Task<string> Handle(DuplicateHandledCommand command, CancellationToken cancellationToken) =>
        Task.FromResult("first");
}

public sealed class SecondDuplicateHandler : ICommandHandler<DuplicateHandledCommand, string>
{
    public Task<string> Handle(DuplicateHandledCommand command, CancellationToken cancellationToken) =>
        Task.FromResult("second");
}
