namespace Trail.E2E.Tests;

public sealed class E2EUser
{
    internal E2EUser(string email, string password, string storageState)
    {
        Email = email;
        Password = password;
        StorageState = storageState;
    }

    public string Email { get; }

    public string Password { get; }

    internal string StorageState { get; }
}
