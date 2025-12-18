namespace Admins.Core.Contract;

public interface IServer
{
    /// <summary>
    /// The unique identifier of the server.
    /// </summary>
    public ulong Id { get; set; }
    /// <summary>
    /// The IP address of the server.
    /// </summary>
    public string IP { get; set; }
    /// <summary>
    /// The port number of the server.
    /// </summary>
    public int Port { get; set; }
    /// <summary>
    /// The hostname of the server.
    /// </summary>
    public string Hostname { get; set; }
    /// <summary>
    /// The GUID of the server.
    /// </summary>
    public string GUID { get; set; }
}