using System.Collections.Concurrent;

namespace PayFlow.MockPaymentProvider.Capture;

public sealed class MockPaymentProviderState
{
    private readonly ConcurrentDictionary<
        Guid,
        MockPaymentScenario> _scenarios = new();

    private readonly ConcurrentDictionary<
        string,
        CaptureOperationState> _captureOperations =
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

        var operation =
            _captureOperations.GetOrAdd(
                idempotencyKey,
                _ =>
                    new CaptureOperationState(
                        fingerprint,
                        _scenarios.GetValueOrDefault(
                            request.PaymentId,
                            MockPaymentScenario.Success)));

        lock (operation.SyncRoot)
        {
            if (operation.Fingerprint
                != fingerprint)
            {
                throw new IdempotencyKeyConflictException(
                    idempotencyKey);
            }

            if (operation.FinalDecision is not null)
            {
                return operation.FinalDecision with
                {
                    AttemptNumber =
                        operation.AttemptCount + 1,
                    IsIdempotentReplay = true
                };
            }

            operation.AttemptCount++;

            return operation.Scenario switch
            {
                MockPaymentScenario.Success =>
                    CompleteSuccess(
                        operation,
                        request.PaymentId),

                MockPaymentScenario.Decline =>
                    CompleteDecline(
                        operation),

                MockPaymentScenario.TimeoutBeforeProcessing =>
                    HandleTimeoutBeforeProcessing(
                        operation,
                        request.PaymentId),

                MockPaymentScenario.TimeoutAfterProcessing =>
                    HandleTimeoutAfterProcessing(
                        operation,
                        request.PaymentId),

                MockPaymentScenario.ServerErrorThenSuccess =>
                    HandleServerErrorThenSuccess(
                        operation,
                        request.PaymentId),

                _ => throw new InvalidOperationException(
                    $"Unsupported mock payment scenario '{operation.Scenario}'.")
            };
        }
    }

    private static MockCaptureDecision CompleteSuccess(
        CaptureOperationState operation,
        Guid paymentId)
    {
        var decision =
            new MockCaptureDecision(
                operation.Scenario,
                MockCaptureOutcome.Succeeded,
                CreateProviderReference(
                    paymentId),
                operation.AttemptCount,
                false);

        operation.FinalDecision =
            decision;

        return decision;
    }

    private static MockCaptureDecision CompleteDecline(
        CaptureOperationState operation)
    {
        var decision =
            new MockCaptureDecision(
                operation.Scenario,
                MockCaptureOutcome.Declined,
                null,
                operation.AttemptCount,
                false);

        operation.FinalDecision =
            decision;

        return decision;
    }

    private static MockCaptureDecision HandleTimeoutBeforeProcessing(
        CaptureOperationState operation,
        Guid paymentId)
    {
        if (operation.AttemptCount == 1)
        {
            return new MockCaptureDecision(
                operation.Scenario,
                MockCaptureOutcome.TimeoutBeforeProcessing,
                null,
                operation.AttemptCount,
                false);
        }

        return CompleteSuccess(
            operation,
            paymentId);
    }

    private static MockCaptureDecision HandleTimeoutAfterProcessing(
        CaptureOperationState operation,
        Guid paymentId)
    {
        var finalDecision =
            new MockCaptureDecision(
                operation.Scenario,
                MockCaptureOutcome.Succeeded,
                CreateProviderReference(
                    paymentId),
                operation.AttemptCount,
                false);

        operation.FinalDecision =
            finalDecision;

        return finalDecision with
        {
            Outcome =
                MockCaptureOutcome.TimeoutAfterProcessing
        };
    }

    private static MockCaptureDecision HandleServerErrorThenSuccess(
        CaptureOperationState operation,
        Guid paymentId)
    {
        if (operation.AttemptCount == 1)
        {
            return new MockCaptureDecision(
                operation.Scenario,
                MockCaptureOutcome.ServerError,
                null,
                operation.AttemptCount,
                false);
        }

        return CompleteSuccess(
            operation,
            paymentId);
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

    private sealed class CaptureOperationState
    {
        public CaptureOperationState(
            CaptureRequestFingerprint fingerprint,
            MockPaymentScenario scenario)
        {
            Fingerprint = fingerprint;
            Scenario = scenario;
        }

        public object SyncRoot { get; } =
            new();

        public CaptureRequestFingerprint Fingerprint { get; }

        public MockPaymentScenario Scenario { get; }

        public int AttemptCount { get; set; }

        public MockCaptureDecision? FinalDecision { get; set; }
    }

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
