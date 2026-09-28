namespace PayFlow.Saga.Application.Checkout;

public interface IPostCaptureCompensationStarter
{
    Task StartAsync(
        PostCaptureCompensationRequest request,
        CancellationToken cancellationToken = default);
}
