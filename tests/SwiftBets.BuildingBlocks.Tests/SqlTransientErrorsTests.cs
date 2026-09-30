using SwiftBets.BuildingBlocks.Resilience;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class SqlTransientErrorsTests
{
    [Theory]
    [InlineData(1205)]
    [InlineData(40613)]
    public void Deadlocks_and_azure_sql_unavailability_are_transient(int number) =>
        SqlTransientErrors.IsTransient(number).ShouldBeTrue();

    [Theory]
    [InlineData(2627)]
    [InlineData(547)]
    public void Constraint_violations_are_not_transient(int number) =>
        SqlTransientErrors.IsTransient(number).ShouldBeFalse();
}
