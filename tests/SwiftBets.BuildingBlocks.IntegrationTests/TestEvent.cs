using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.IntegrationTests;

public sealed record TestEvent(string Name, int Value) : IEventContract
{
    public static string EventType => "test.thing-happened";

    public static int EventVersion => 1;
}
