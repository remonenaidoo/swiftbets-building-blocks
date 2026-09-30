using SwiftBets.BuildingBlocks.Core;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class CorrelationContextTests
{
    [Fact]
    public void Scope_sets_and_restores_the_correlation_id()
    {
        using (CorrelationContext.Begin("outer"))
        {
            using (CorrelationContext.Begin("inner"))
            {
                CorrelationContext.CorrelationId.ShouldBe("inner");
            }

            CorrelationContext.CorrelationId.ShouldBe("outer");
        }
    }

    [Theory]
    [InlineData("abc-123_X", true)]
    [InlineData("has space", false)]
    [InlineData("<script>", false)]
    public void Only_safe_inbound_ids_are_accepted(string candidate, bool expected) =>
        CorrelationContext.IsValid(candidate).ShouldBe(expected);
}
