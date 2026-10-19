namespace Trail.Application.Abstractions;

public interface IDeletionImpactContributor
{
    Task<IReadOnlyList<DeletionImpactItem>> GetImpactAsync(DeletionScope scope, CancellationToken cancellationToken = default);
}
