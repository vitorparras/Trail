using Trail.Domain.Common;

namespace Trail.Domain.Practice;

public sealed class QuestionCount : IEquatable<QuestionCount>
{
    private QuestionCount()
    {
    }

    private QuestionCount(int total, int correct, int wrong)
    {
        Total = total;
        Correct = correct;
        Wrong = wrong;
    }

    public int Total { get; private set; }

    public int Correct { get; private set; }

    public int Wrong { get; private set; }

    public int Blank => Total - Correct - Wrong;

    public static Result<QuestionCount> Create(int total, int correct, int wrong)
    {
        var notification = new Notification()
            .AddErrorIf(total <= 0, "practice.question_count.total_must_be_positive", nameof(Total))
            .AddErrorIf(correct < 0, "practice.question_count.correct_must_not_be_negative", nameof(Correct))
            .AddErrorIf(wrong < 0, "practice.question_count.wrong_must_not_be_negative", nameof(Wrong));

        if (!notification.HasErrors)
        {
            notification.AddErrorIf((long)correct + wrong > total, "practice.question_count.answers_exceed_total", nameof(Correct));
        }

        return notification.ToResult(() => new QuestionCount(total, correct, wrong));
    }

    public bool Equals(QuestionCount? other) =>
        other is not null && Total == other.Total && Correct == other.Correct && Wrong == other.Wrong;

    public override bool Equals(object? obj) => obj is QuestionCount other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Total, Correct, Wrong);

    public static bool operator ==(QuestionCount? left, QuestionCount? right) => Equals(left, right);

    public static bool operator !=(QuestionCount? left, QuestionCount? right) => !Equals(left, right);
}
