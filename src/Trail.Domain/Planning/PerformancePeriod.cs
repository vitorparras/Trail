
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public sealed class PerformancePeriod : IEquatable<PerformancePeriod>
{
    private PerformancePeriod()
    {
    }

    private PerformancePeriod(PerformancePeriodKind kind, int? days, DateOnly? startDate, DateOnly? endDate)
    {
        Kind = kind;
        Days = days;
        StartDate = startDate;
        EndDate = endDate;
    }

    public PerformancePeriodKind Kind { get; private set; }

    public int? Days { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public static PerformancePeriod All() => new(PerformancePeriodKind.All, null, null, null);

    public static Result<PerformancePeriod> LastDays(int days)
    {
        if (days <= 0)
        {
            return Result.Invalid(Message.Error("planning.period.days_must_be_positive", nameof(Days)));
        }

        return Result.Ok(new PerformancePeriod(PerformancePeriodKind.LastDays, days, null, null));
    }

    public static Result<PerformancePeriod> DateRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            return Result.Invalid(Message.Error("planning.period.invalid_date_range", nameof(EndDate)));
        }

        return Result.Ok(new PerformancePeriod(PerformancePeriodKind.DateRange, null, startDate, endDate));
    }

    public static Result<PerformancePeriod> Create(PerformancePeriodKind kind, int? days = null, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        return kind switch
        {
            PerformancePeriodKind.All => Result.Ok(All()),
            PerformancePeriodKind.LastDays => days is > 0
                ? Result.Ok(new PerformancePeriod(kind, days, null, null))
                : Result.Invalid(Message.Error("planning.period.days_must_be_positive", nameof(Days))),
            PerformancePeriodKind.DateRange => startDate.HasValue && endDate.HasValue && startDate.Value <= endDate.Value
                ? Result.Ok(new PerformancePeriod(kind, null, startDate, endDate))
                : Result.Invalid(Message.Error("planning.period.invalid_date_range", nameof(EndDate))),
            _ => Result.Invalid(Message.Error("planning.period.invalid_kind", nameof(Kind))),
        };
    }

    public bool Includes(DateOnly entryDate, DateOnly referenceToday)
    {
        return Kind switch
        {
            PerformancePeriodKind.All => true,
            PerformancePeriodKind.LastDays => entryDate >= referenceToday.AddDays(-(Days!.Value - 1)) && entryDate <= referenceToday,
            PerformancePeriodKind.DateRange => entryDate >= StartDate!.Value && entryDate <= EndDate!.Value,
            _ => false,
        };
    }

    public bool Equals(PerformancePeriod? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Kind == other.Kind
            && Days == other.Days
            && StartDate == other.StartDate
            && EndDate == other.EndDate;
    }

    public override bool Equals(object? obj) => obj is PerformancePeriod other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, Days, StartDate, EndDate);

    public static bool operator ==(PerformancePeriod? left, PerformancePeriod? right) => Equals(left, right);

    public static bool operator !=(PerformancePeriod? left, PerformancePeriod? right) => !Equals(left, right);
}
