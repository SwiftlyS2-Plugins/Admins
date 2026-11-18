namespace Admins.Contract;

public interface IServer
{
    /// <summary>
    /// The unique identifier of the server.
    /// </summary>
    long Id { get; set; }

    /// <summary>
    /// The IP address of the server.
    /// </summary>
    string IP { get; set; }

    /// <summary>
    /// The port number of the server.
    /// </summary>
    int Port { get; set; }

    /// <summary>
    /// The hostname of the server.
    /// </summary>
    string Hostname { get; set; }

    /// <summary>
    /// The GUID of the server.
    /// </summary>
    string GUID { get; set; }
}
