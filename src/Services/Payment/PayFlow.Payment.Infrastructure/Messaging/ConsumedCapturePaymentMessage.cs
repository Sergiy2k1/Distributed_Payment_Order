using PayFlow.Payment.Application.Capture;

namespace PayFlow.Payment.Infrastructure.Messaging;

public sealed record ConsumedCapturePaymentMessage(
    CapturePaymentMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
