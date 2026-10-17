namespace Trail.E2E.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ETestCollectionDefinition : ICollectionFixture<DockerAppFixture>
{
    public const string Name = "E2E";
}
