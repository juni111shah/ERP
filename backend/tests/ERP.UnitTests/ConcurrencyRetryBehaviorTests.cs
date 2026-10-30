using ERP.Application.Abstractions;
using ERP.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.UnitTests;

public class ConcurrencyRetryBehaviorTests
{
    private sealed record Retryable : IRetryOnConcurrency;
    private sealed record Plain;

    private sealed class FakeDbErrors : IDbErrors { public bool IsUniqueViolation(Exception ex, string indexName) => ex.Message.Contains(indexName); }

    private sealed class FakeUow : IUnitOfWork
    {
        public int Discards;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public void DiscardChanges() => Discards++;
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default) => work(ct);
    }

    private sealed class FakeReset : IResetOnRetry { public int Resets; public void ResetForRetry() => Resets++; }

    private static RequestHandlerDelegate<string> Throwing(int times, out Func<int> calls)
    {
        var n = 0;
        calls = () => n;
        return () => ++n <= times ? throw new DbUpdateConcurrencyException("race") : Task.FromResult("ok");
    }

    [Fact]
    public async Task A_retryable_command_is_run_again_until_it_succeeds()
    {
        var uow = new FakeUow(); var reset = new FakeReset();
        var b = new ConcurrencyRetryBehavior<Retryable, string>(uow, [reset], new FakeDbErrors(), NullLogger<ConcurrencyRetryBehavior<Retryable, string>>.Instance);
        var result = await b.Handle(new Retryable(), Throwing(2, out var calls), default);
        Assert.Equal("ok", result);
        Assert.Equal(3, calls());
        Assert.Equal((2, 2), (uow.Discards, reset.Resets));
    }

    [Fact]
    public async Task A_conflict_that_persists_after_three_attempts_is_thrown()
    {
        var b = new ConcurrencyRetryBehavior<Retryable, string>(new FakeUow(), [], new FakeDbErrors(), NullLogger<ConcurrencyRetryBehavior<Retryable, string>>.Instance);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.Handle(new Retryable(), Throwing(10, out var calls), default));
    }

    [Fact]
    public async Task A_command_without_the_marker_is_never_retried()
    {
        var uow = new FakeUow();
        var b = new ConcurrencyRetryBehavior<Plain, string>(uow, [], new FakeDbErrors(), NullLogger<ConcurrencyRetryBehavior<Plain, string>>.Instance);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.Handle(new Plain(), Throwing(1, out var calls), default));
        Assert.Equal(0, uow.Discards);
    }

    [Fact]
    public async Task A_duplicate_document_number_is_retried_but_other_unique_violations_are_not()
    {
        var b = new ConcurrencyRetryBehavior<Retryable, string>(new FakeUow(), [], new FakeDbErrors(), NullLogger<ConcurrencyRetryBehavior<Retryable, string>>.Instance);
        var n = 0;
        var result = await b.Handle(new Retryable(), () => ++n < 3 ? throw new DbUpdateException("dup IX_Receipts_OrgId_Number") : Task.FromResult("ok"), default);
        Assert.Equal("ok", result);

        var other = 0;
        await Assert.ThrowsAsync<DbUpdateException>(() => b.Handle(new Retryable(), () => { other++; throw new DbUpdateException("dup IX_Parties_Email"); }, default));
        Assert.Equal(1, other);
    }
}
