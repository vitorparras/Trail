namespace Trail.Web.Dashboard;

public interface IDashboardBlock
{
    int Order { get; }

    Type ViewComponent { get; }
}
