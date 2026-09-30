using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using SwiftBets.BuildingBlocks.Resilience;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, byte[]> _producer;
    private readonly ResiliencePipeline _pipeline;
    private readonly string _environment;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ResiliencePipelineProvider<string> pipelines)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = options.Value.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            LingerMs = 5,
            CompressionType = CompressionType.Lz4,
            MessageTimeoutMs = 30_000,
        };
        _producer = new ProducerBuilder<string, byte[]>(config).Build();
        _pipeline = pipelines.GetPipeline(ResiliencePipelineNames.KafkaProduce);
        _environment = options.Value.Environment;
    }

    public Task PublishAsync<TPayload>(string topicBase, string key, EventEnvelope<TPayload> envelope, CancellationToken cancellationToken)
        where TPayload : IEventContract =>
        PublishRawAsync(
            new OutgoingMessage(TopicName.For(topicBase, _environment).Value, key, EnvelopeSerializer.Serialize(envelope), EnvelopeSerializer.Headers(envelope)),
            cancellationToken);

    public async Task PublishRawAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        var headers = new Headers();
        foreach (var (name, value) in message.Headers)
        {
            headers.Add(name, Encoding.UTF8.GetBytes(value));
        }

        var kafkaMessage = new Message<string, byte[]> { Key = message.Key, Value = message.Value, Headers = headers };
        await _pipeline.ExecuteAsync(
            async token => await _producer.ProduceAsync(message.Topic, kafkaMessage, token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
