namespace Admins.Contract;

/// <summary>
/// Shared interface key: <c>Admins.Server.V2</c>.
/// </summary>
public interface IServerManager
{
    /// <summary>
    /// Gets the GUID that identifies this server in the <c>servers</c> table and in every
    /// <c>Servers</c> / <c>Server</c> column.
    /// </summary>
    string GetServerGUID();

    /// <summary>
    /// Sets and persists the server GUID. Must be a valid GUID; invalid values are ignored.
    /// </summary>
    void SetServerGUID(string serverGUID);
}
