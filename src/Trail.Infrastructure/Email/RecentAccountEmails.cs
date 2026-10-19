using System.Security.Cryptography;
using System.Text;
using Trail.Application.Accounts;

namespace Trail.Infrastructure.Email;

internal sealed class RecentAccountEmails
{
    private readonly int _capacity;
    private readonly TimeSpan _window;
    private readonly Dictionary<string, LinkedListNode<Entry>> _byKey = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _byAge = new();

    public RecentAccountEmails(int capacity, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        _capacity = capacity;
        _window = window;
    }

    public int Count => _byKey.Count;

    public static string KeyFor(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var recipient = SHA256.HashData(Encoding.UTF8.GetBytes(message.ToAddress.Trim().ToUpperInvariant()));

        return string.Concat(message.Kind, ":", Convert.ToHexString(recipient));
    }

    public bool Contains(string key, DateTimeOffset now)
    {
        RemoveExpired(now);

        return _byKey.ContainsKey(key);
    }

    public void Remember(string key, DateTimeOffset now)
    {
        if (_byKey.Remove(key, out var previous))
        {
            _byAge.Remove(previous);
        }

        RemoveExpired(now);
        while (_byKey.Count >= _capacity)
        {
            RemoveOldest();
        }

        _byKey[key] = _byAge.AddLast(new Entry(key, now));
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        while (_byAge.First is { } oldest && now - oldest.Value.SentAt >= _window)
        {
            RemoveOldest();
        }
    }

    private void RemoveOldest()
    {
        var oldest = _byAge.First!.Value;
        _byAge.RemoveFirst();
        _byKey.Remove(oldest.Key);
    }

    private sealed record Entry(string Key, DateTimeOffset SentAt);
}
