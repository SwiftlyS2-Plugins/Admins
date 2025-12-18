namespace Admins.Core.Contract;

public interface IServerManager
{
    /// <summary>
    /// Sets the server GUID. It needs to be a valid Guid string value.
    /// </summary>
    /// <param name="serverGUID">The unique identifier for the server.</param>
    public void SetServerGUID(string serverGUID);

    /// <summary>
    /// Gets the server GUID.
    /// </summary>
    /// <returns>The unique identifier for the server.</returns>
    public string GetServerGUID();
}