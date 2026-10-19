namespace Trail.Application.Abstractions;

public interface IPlanActivityContributor
{
    Task<IReadOnlyList<PlanActivity>> GetLatestActivityAsync(CancellationToken cancellationToken = default);
}
