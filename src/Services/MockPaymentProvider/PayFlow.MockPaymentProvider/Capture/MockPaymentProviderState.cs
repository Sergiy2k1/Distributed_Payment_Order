using System.Collections.Concurrent;

namespace PayFlow.MockPaymentProvider.Capture;

public sealed class MockPaymentProviderState
{
    private readonly ConcurrentDictionary<
        Guid,
        MockPaymentScenario> _scenarios = new();

    private readonly ConcurrentDictionary<
        string,
        StoredCaptureDecision> _captureDecisions =
        new(StringComparer.Ordinal);

    public void ConfigureScenario(
        Guid paymentId,
        MockPaymentScenario scenario)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException(
                "PaymentId cannot be empty.",
                nameof(paymentId));
        }

        _scenarios[paymentId] = scenario;
    }

    public bool ResetScenario(
        Guid paymentId)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException(
                "PaymentId cannot be empty.",
                nameof(paymentId));
        }

        return _scenarios.TryRemove(
            paymentId,
            out _);
    }

    public MockCaptureDecision Capture(
        string idempotencyKey,
        CapturePaymentRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            idempotencyKey);
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        var fingerprint =
            CaptureRequestFingerprint.Create(
                request);

        if (_captureDecisions.TryGetValue(
                idempotencyKey,
                out var existing))
        {
            if (existing.Fingerprint != fingerprint)
            {
                throw new IdempotencyKeyConflictException(
                    idempotencyKey);
            }

            return existing.Decision with
            {
                IsIdempotentReplay = true
            };
        }

        var scenario =
            _scenarios.GetValueOrDefault(
                request.PaymentId,
                MockPaymentScenario.Success);

        var decision =
            scenario switch
            {
                MockPaymentScenario.Success =>
                    new MockCaptureDecision(
                        scenario,
                        CreateProviderReference(
                            request.PaymentId),
                        false),

                MockPaymentScenario.Decline =>
                    new MockCaptureDecision(
                        scenario,
                        null,
                        false),

                _ => throw new InvalidOperationException(
                    $"Unsupported mock payment scenario '{scenario}'.")
            };

        var stored =
            new StoredCaptureDecision(
                fingerprint,
                decision);

        var actual =
            _captureDecisions.GetOrAdd(
                idempotencyKey,
                stored);

        if (actual.Fingerprint != fingerprint)
        {
            throw new IdempotencyKeyConflictException(
                idempotencyKey);
        }

        return ReferenceEquals(
                actual,
                stored)
            ? decision
            : actual.Decision with
            {
                IsIdempotentReplay = true
            };
    }

    private static void ValidateRequest(
        CapturePaymentRequest request)
    {
        if (request.PaymentId == Guid.Empty
            || request.OrderId == Guid.Empty)
        {
            throw new ArgumentException(
                "PaymentId and OrderId must be non-empty.",
                nameof(request));
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Amount,
                "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(
                request.Currency)
            || request.Currency.Trim().Length != 3)
        {
            throw new ArgumentException(
                "Currency must be a three-letter code.",
                nameof(request));
        }
    }

    private static string CreateProviderReference(
        Guid paymentId)
    {
        return $"mock-capture-{paymentId:N}";
    }

    private sealed record StoredCaptureDecision(
        CaptureRequestFingerprint Fingerprint,
        MockCaptureDecision Decision);

    private sealed record CaptureRequestFingerprint(
        Guid PaymentId,
        Guid OrderId,
        decimal Amount,
        string Currency)
    {
        public static CaptureRequestFingerprint Create(
            CapturePaymentRequest request)
        {
            return new CaptureRequestFingerprint(
                request.PaymentId,
                request.OrderId,
                request.Amount,
                request.Currency
                    .Trim()
                    .ToUpperInvariant());
        }
    }
}
