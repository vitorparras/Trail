using System.Collections.Immutable;

namespace Trail.Web.Results;

public sealed class FieldTargetMap
{
    public static readonly FieldTargetMap Empty = new(ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase), string.Empty);

    private readonly ImmutableDictionary<string, string> _fields;

    private FieldTargetMap(ImmutableDictionary<string, string> fields, string formLevelKey)
    {
        _fields = fields;
        FormLevelKey = formLevelKey;
    }

    public string FormLevelKey { get; }

    public FieldTargetMap Field(string target, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        return new FieldTargetMap(_fields.SetItem(target, field), FormLevelKey);
    }

    public FieldTargetMap Prefixed(string prefix, params string[] targets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(targets);

        return targets.Aggregate(this, (map, target) => map.Field(target, $"{prefix}.{target}"));
    }

    public FieldTargetMap FormLevel(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new FieldTargetMap(_fields, key);
    }

    public bool TryResolve(string target, out string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        return _fields.TryGetValue(target, out field!);
    }
}
