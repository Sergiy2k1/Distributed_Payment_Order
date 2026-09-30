using System.Threading.Channels;

namespace PayFlow.MockPaymentProvider.Webhooks;

public sealed class MockWebhookDeliveryQueue
{
    private readonly Channel<QueuedProviderWebhook> _channel =
        Channel.CreateUnbounded<QueuedProviderWebhook>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

    public ValueTask EnqueueAsync(
        QueuedProviderWebhook webhook,
        CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(webhook, cancellationToken);

    public IAsyncEnumerable<QueuedProviderWebhook> ReadAllAsync(
        CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public bool TryRead(out QueuedProviderWebhook? webhook) =>
        _channel.Reader.TryRead(out webhook);
}
