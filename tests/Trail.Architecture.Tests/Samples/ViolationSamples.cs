using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Violations.Aggregates.Common;
using Trail.Infrastructure.Persistence;
using Trail.Web;

namespace Trail.Domain.Violations
{
    public class DomainEntityReferencingEfCore
    {
        public DbContext? Context { get; set; }
    }
}

namespace Trail.Domain.Violations.Layers
{
    public class DomainEntityReferencingApplication
    {
        public ICurrentUser? CurrentUser { get; set; }
    }

    public class DomainEntityReferencingInfrastructure
    {
        public AppDbContext? Context { get; set; }
    }

    public class DomainEntityReferencingWeb
    {
        public SharedResource? Resource { get; set; }
    }
}

namespace Trail.Application.Violations.Layers
{
    public class ApplicationServiceReferencingInfrastructure
    {
        public AppDbContext? Context { get; set; }
    }

    public class ApplicationServiceReferencingWeb
    {
        public SharedResource? Resource { get; set; }
    }
}

namespace Trail.Infrastructure.Violations.Layers
{
    public class InfrastructureServiceReferencingWeb
    {
        public SharedResource? Resource { get; set; }
    }
}

namespace Trail.Web.Violations
{
    public class WebComponentReferencingInfrastructure
    {
        public AppDbContext? Context { get; set; }
    }

    public class WebComponentUsingSqliteConnection
    {
        public SqliteConnection? Connection { get; set; }
    }
}

namespace Trail.Application.Violations.Restricted
{
    public class ApplicationComponentUsingNpgsqlConnection
    {
        public NpgsqlConnection? Connection { get; set; }
    }
}

namespace Trail.Web.Pages.Violations
{
    public class PageModelReferencingAppDbContext : PageModel
    {
        public IAppDbContext? Context { get; set; }
    }

    public class PageModelReferencingConcreteDbContext : PageModel
    {
        public AppDbContext? Context { get; set; }
    }
}

namespace Trail.Domain.Violations.Aggregates.Common
{
    public readonly record struct FirstAggregateId(Guid Value) : ITypedId<FirstAggregateId>
    {
        public static FirstAggregateId From(Guid value) => new(value);
        public static FirstAggregateId New() => new(Guid.CreateVersion7());
    }

    public readonly record struct SecondAggregateId(Guid Value) : ITypedId<SecondAggregateId>
    {
        public static SecondAggregateId From(Guid value) => new(value);
        public static SecondAggregateId New() => new(Guid.CreateVersion7());
    }

    public sealed class SampleSecondAggregate(SecondAggregateId id, UserId ownerId) : AggregateRoot<SecondAggregateId>(id, ownerId);
}

namespace Trail.Domain.Violations.Aggregates.DirectProperty
{
    public sealed class SampleFirstAggregate(FirstAggregateId id, UserId ownerId) : AggregateRoot<FirstAggregateId>(id, ownerId)
    {
        public SampleSecondAggregate? Other { get; set; }
    }
}

namespace Trail.Domain.Violations.Aggregates.EntityInsideAggregate
{
    public sealed class SampleEntityInsideAggregate
    {
        public SampleSecondAggregate? Other { get; set; }
    }
}

namespace Trail.Domain.Violations.Aggregates.ConstructorParam
{
    public sealed class AggregateWithConstructorAggregate(SampleSecondAggregate other, FirstAggregateId id, UserId ownerId) : AggregateRoot<FirstAggregateId>(id, ownerId)
    {
        public SampleSecondAggregate OtherAggregate { get; } = other;
    }
}

namespace Trail.Domain.Violations.Aggregates.MethodParam
{
    public sealed class AggregateWithMethodParameter(FirstAggregateId id, UserId ownerId) : AggregateRoot<FirstAggregateId>(id, ownerId)
    {
        public void Attach(SampleSecondAggregate other)
        {
            ArgumentNullException.ThrowIfNull(other);
            if (Id.Value == Guid.Empty)
            {
                throw new InvalidOperationException();
            }
        }
    }
}

namespace Trail.Domain.Violations.Aggregates.MethodReturn
{
    public sealed class AggregateWithMethodReturn(FirstAggregateId id, UserId ownerId) : AggregateRoot<FirstAggregateId>(id, ownerId)
    {
        public SampleSecondAggregate? GetOther() => Id.Value != Guid.Empty ? null : null;
    }
}

namespace Trail.Application.Planning.Violations
{
    public class PlanningService;
}

namespace Trail.Application.Practice.Violations
{
    public class PracticeServiceReferencingPlanning
    {
        public Trail.Application.Planning.Violations.PlanningService? Planning { get; set; }
    }
}

namespace Trail.Application.Violations.Handlers.Unsealed
{
    public class UnsealedSampleHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<Result> HandleAsync() => Task.FromResult(_id != Guid.Empty ? Result.Ok() : Result.NotFound());
    }
}

namespace Trail.Application.Violations.Handlers.Internal
{
    internal sealed class InternalSampleHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<Result> HandleAsync() => Task.FromResult(_id != Guid.Empty ? Result.Ok() : Result.NotFound());
    }
}

namespace Trail.Application.Violations.Handlers.ExtraMethod
{
    public sealed class ExtraPublicMethodSampleHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<Result> HandleAsync() => Task.FromResult(_id != Guid.Empty ? Result.Ok() : Result.NotFound());

        public bool AdditionalMethod() => _id != Guid.Empty;
    }
}

namespace Trail.Application.Violations.Handlers.InvalidReturn
{
    public sealed class InvalidReturnTypeSampleHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<int> HandleAsync() => Task.FromResult(_id != Guid.Empty ? 1 : 0);
    }
}

namespace Trail.Application.Violations.Handlers.WrongMethodName
{
    public sealed class WrongMethodNameSampleHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<Result> ExecuteAsync() => Task.FromResult(_id != Guid.Empty ? Result.Ok() : Result.NotFound());
    }
}

namespace Trail.Application.Violations.Handlers.Valid
{
    public sealed class ValidSampleHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<Result> HandleAsync() => Task.FromResult(_id != Guid.Empty ? Result.Ok() : Result.NotFound());
    }
}

namespace Trail.Application.Violations.Handlers.ValidVariants
{
    public sealed class ValidResultHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Result HandleAsync() => _id != Guid.Empty ? Result.Ok() : Result.NotFound();
    }

    public sealed class ValidResultOfTHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Result<int> HandleAsync() => _id != Guid.Empty ? Result.Ok(42) : Result.NotFound();
    }

    public sealed class ValidTaskResultOfTHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<Result<string>> HandleAsync() => Task.FromResult<Result<string>>(_id != Guid.Empty ? Result.Ok("valid") : Result.NotFound());
    }
}

namespace Trail.Domain.Violations.Conventions.EntitySetter
{
    public sealed class EntityWithPublicSetter(UserId id, UserId ownerId) : AggregateRoot<UserId>(id, ownerId)
    {
        public string Name { get; set; } = string.Empty;
    }
}

namespace Trail.Domain.Violations.Conventions.EntityInit
{
    public sealed class EntityWithPublicInit(UserId id, UserId ownerId) : AggregateRoot<UserId>(id, ownerId)
    {
        public string Title { get; init; } = string.Empty;
    }
}

namespace Trail.Domain.Violations.Conventions.EntityInsideAggregate
{
    public sealed class EntityInsideAggregateWithPublicSetter
    {
        public string Title { get; set; } = string.Empty;
    }

    public sealed class EntityInsideAggregateWithPublicInit
    {
        public string Title { get; init; } = string.Empty;
    }
}

namespace Trail.Domain.Violations.Conventions.UnsealedEvent
{
    public record UnsealedSampleEvent : IDomainEvent;
}

namespace Trail.Domain.Violations.Conventions.NonRecordEvent
{
    public sealed class NonRecordSampleEvent : IDomainEvent;
}

namespace Trail.Domain.Violations.Conventions.ValidEvent
{
    public sealed record ValidSampleEvent : IDomainEvent;
}

namespace Trail.Domain.Violations.Events.Orphan
{
    public sealed record OrphanSampleEvent : IDomainEvent;
}

namespace Trail.Domain.Violations.Events.Consumed
{
    public sealed record ConsumedSampleEvent : IDomainEvent;
}

namespace Trail.Application.Violations.Consumers
{
    public sealed class ConsumedSampleEventConsumer : IDomainEventConsumer<Trail.Domain.Violations.Events.Consumed.ConsumedSampleEvent>
    {
        public Task ConsumeAsync(Trail.Domain.Violations.Events.Consumed.ConsumedSampleEvent domainEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

namespace Trail.Domain.Violations.TypedIds.NonRecord
{
    public readonly struct PlainStructTypedId : ITypedId<PlainStructTypedId>
    {
        public Guid Value { get; }
        public PlainStructTypedId(Guid value) => Value = value;
        public static PlainStructTypedId From(Guid value) => new(value);
        public static PlainStructTypedId New() => new(Guid.CreateVersion7());
    }
}

namespace Trail.Domain.Violations.TypedIds.MissingNew
{
    public readonly record struct TypedIdWithoutNewMethod(Guid Value) : ITypedId<TypedIdWithoutNewMethod>
    {
        public static TypedIdWithoutNewMethod From(Guid value) => new(value);
    }
}

namespace Trail.Domain.Violations.TypedIds.MissingFrom
{
    public readonly record struct TypedIdWithExplicitFrom(Guid Value) : ITypedId<TypedIdWithExplicitFrom>
    {
        static TypedIdWithExplicitFrom ITypedId<TypedIdWithExplicitFrom>.From(Guid value) => new(value);
        public static TypedIdWithExplicitFrom New() => new(Guid.CreateVersion7());
    }
}

namespace Trail.Domain.Violations.TypedIds.Mutable
{
    public record struct MutableTypedId : ITypedId<MutableTypedId>
    {
        public Guid Value { get; set; }
        public static MutableTypedId From(Guid value) => new() { Value = value };
        public static MutableTypedId New() => new() { Value = Guid.CreateVersion7() };
    }
}

namespace Trail.Domain.Violations.TypedIds.WrongValueType
{
    public readonly record struct TypedIdWithWrongValueType(int Value)
    {
        public static TypedIdWithWrongValueType From(int value) => new(value);
        public static TypedIdWithWrongValueType New() => new(1);
    }
}
