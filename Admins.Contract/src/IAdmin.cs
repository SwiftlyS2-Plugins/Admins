namespace Admins.Contract;

public interface IAdmin
{
    /// <summary>
    /// The unique identifier of the admin.
    /// </summary>
    long Id { get; set; }

    /// <summary>
    /// The SteamID64 of the admin.
    /// </summary>
    long SteamId64 { get; set; }

    /// <summary>
    /// The username of the admin. Unique across the database.
    /// </summary>
    string Username { get; set; }

    /// <summary>
    /// The immunity level of the admin.
    /// </summary>
    int Immunity { get; set; }

    /// <summary>
    /// The permissions assigned directly to the admin.
    /// </summary>
    List<string> Permissions { get; set; }

    /// <summary>
    /// The names of the groups the admin belongs to.
    /// </summary>
    List<string> Groups { get; set; }

    /// <summary>
    /// The GUIDs of the servers the admin has access to.
    /// </summary>
    List<string> Servers { get; set; }
}
