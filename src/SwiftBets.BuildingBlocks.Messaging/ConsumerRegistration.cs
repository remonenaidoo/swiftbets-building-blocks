namespace SwiftBets.BuildingBlocks.Messaging;

public sealed record ConsumerRegistration(string TopicBase, string GroupId);
