using System.Collections.Concurrent;
using PayFlow.MockPaymentProvider.Capture;

namespace PayFlow.MockPaymentProvider.Refund;

public sealed class MockRefundProviderState
{
    private readonly ConcurrentDictionary<
        Guid,
        MockRefundScenario> _scenarios = new();

    private readonly ConcurrentDictionary<
        string,
        RefundOperationState> _refundOperations =
        new(StringComparer.Ordinal);

    public void ConfigureScenario(
        Guid refundId,
        MockRefundScenario scenario)
    {
        if (refundId == Guid.Empty)
        {
            throw new ArgumentException(
                "RefundId cannot be empty.",
                nameof(refundId));
        }

        _scenarios[refundId] = scenario;
    }

    public bool ResetScenario(
        Guid refundId)
    {
        if (refundId == Guid.Empty)
        {
            throw new ArgumentException(
                "RefundId cannot be empty.",
                nameof(refundId));
        }

        return _scenarios.TryRemove(
            refundId,
            out _);
    }

    public MockRefundDecision Refund(
        string idempotencyKey,
        RefundPaymentRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            idempotencyKey);
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        var fingerprint =
            RefundRequestFingerprint.Create(
                request);

        var operation =
            _refundOperations.GetOrAdd(
                idempotencyKey,
                _ =>
                    new RefundOperationState(
                        fingerprint,
                        _scenarios.GetValueOrDefault(
                            request.RefundId,
                            MockRefundScenario.Success)));

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
                MockRefundScenario.Success =>
                    CompleteSuccess(
                        operation,
                        request.RefundId),

                MockRefundScenario.Reject =>
                    CompleteReject(
                        operation),

                MockRefundScenario.TimeoutBeforeProcessing =>
                    HandleTimeoutBeforeProcessing(
                        operation,
                        request.RefundId),

                MockRefundScenario.TimeoutAfterProcessing =>
                    HandleTimeoutAfterProcessing(
                        operation,
                        request.RefundId),

                MockRefundScenario.ServerErrorThenSuccess =>
                    HandleServerErrorThenSuccess(
                        operation,
                        request.RefundId),

                _ => throw new InvalidOperationException(
                    $"Unsupported mock refund scenario '{operation.Scenario}'.")
            };
        }
    }

    private static MockRefundDecision CompleteSuccess(
        RefundOperationState operation,
        Guid refundId)
    {
        var decision =
            new MockRefundDecision(
                operation.Scenario,
                MockRefundOutcome.Succeeded,
                CreateProviderReference(
                    refundId),
                operation.AttemptCount,
                false);

        operation.FinalDecision =
            decision;

        return decision;
    }

    private static MockRefundDecision CompleteReject(
        RefundOperationState operation)
    {
        var decision =
            new MockRefundDecision(
                operation.Scenario,
                MockRefundOutcome.Rejected,
                null,
                operation.AttemptCount,
                false);

        operation.FinalDecision =
            decision;

        return decision;
    }

    private static MockRefundDecision HandleTimeoutBeforeProcessing(
        RefundOperationState operation,
        Guid refundId)
    {
        if (operation.AttemptCount == 1)
        {
            return new MockRefundDecision(
                operation.Scenario,
                MockRefundOutcome.TimeoutBeforeProcessing,
                null,
                operation.AttemptCount,
                false);
        }

        return CompleteSuccess(
            operation,
            refundId);
    }

    private static MockRefundDecision HandleTimeoutAfterProcessing(
        RefundOperationState operation,
        Guid refundId)
    {
        var finalDecision =
            new MockRefundDecision(
                operation.Scenario,
                MockRefundOutcome.Succeeded,
                CreateProviderReference(
                    refundId),
                operation.AttemptCount,
                false);

        operation.FinalDecision =
            finalDecision;

        return finalDecision with
        {
            Outcome =
                MockRefundOutcome.TimeoutAfterProcessing
        };
    }

    private static MockRefundDecision HandleServerErrorThenSuccess(
        RefundOperationState operation,
        Guid refundId)
    {
        if (operation.AttemptCount == 1)
        {
            return new MockRefundDecision(
                operation.Scenario,
                MockRefundOutcome.ServerError,
                null,
                operation.AttemptCount,
                false);
        }

        return CompleteSuccess(
            operation,
            refundId);
    }

    private static void ValidateRequest(
        RefundPaymentRequest request)
    {
        if (request.PaymentId == Guid.Empty
            || request.OrderId == Guid.Empty
            || request.RefundId == Guid.Empty)
        {
            throw new ArgumentException(
                "PaymentId, OrderId and RefundId must be non-empty.",
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
        Guid refundId)
    {
        return $"mock-refund-{refundId:N}";
    }

    private sealed class RefundOperationState
    {
        public RefundOperationState(
            RefundRequestFingerprint fingerprint,
            MockRefundScenario scenario)
        {
            Fingerprint = fingerprint;
            Scenario = scenario;
        }

        public object SyncRoot { get; } =
            new();

        public RefundRequestFingerprint Fingerprint { get; }

        public MockRefundScenario Scenario { get; }

        public int AttemptCount { get; set; }

        public MockRefundDecision? FinalDecision { get; set; }
    }

    private sealed record RefundRequestFingerprint(
        Guid PaymentId,
        Guid OrderId,
        Guid RefundId,
        decimal Amount,
        string Currency)
    {
        public static RefundRequestFingerprint Create(
            RefundPaymentRequest request)
        {
            return new RefundRequestFingerprint(
                request.PaymentId,
                request.OrderId,
                request.RefundId,
                request.Amount,
                request.Currency
                    .Trim()
                    .ToUpperInvariant());
        }
    }
}
