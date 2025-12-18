namespace Admins.Core.Contract;

public interface IAdmin
{
    /// <summary>
    /// The unique identifier of the admin.
    /// </summary>
    public ulong Id { get; set; }
    /// <summary>
    /// The SteamID64 of the admin.
    /// </summary>
    public long SteamId64 { get; set; }
    /// <summary>
    /// The username of the admin.
    /// </summary>
    public string Username { get; set; }
    /// <summary>
    /// The immunity level of the admin.
    /// </summary>
    public uint Immunity { get; set; }
    /// <summary>
    /// The permissions assigned to the admin.
    /// </summary>
    public List<string> Permissions { get; set; }
    /// <summary>
    /// The groups the admin belongs to.
    /// </summary>
    public List<string> Groups { get; set; }
    /// <summary>
    /// The servers the admin has access to.
    /// </summary>
    public List<string> Servers { get; set; }
}