namespace SwiftBets.BuildingBlocks.Messaging;

/// <summary>A fully serialized message addressed to a concrete topic name.</summary>
public sealed record OutgoingMessage(string Topic, string Key, byte[] Value, IReadOnlyDictionary<string, string> Headers);
