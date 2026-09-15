namespace MaintainPro.Application.WorkOrders;

/// <summary>Provider-specific conflict classification and cleanup after an occurrence transaction.</summary>
public interface IGenerationConcurrency
{
    void ResetTracking();
    bool IsRetryable(Exception exception);
}
