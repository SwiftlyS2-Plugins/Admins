using Admins.Core.Contract;

namespace Admins.Core.Server;

public class ServerManager : IServerManager
{
    private ServerLoader? _serverLoader;

    public ServerManager(ServerLoader loader)
    {
        _serverLoader = loader;
    }

    public string GetServerGUID()
    {
        return ServerLoader.ServerGUID;
    }

    public void SetServerGUID(string serverGUID)
    {
        _serverLoader!.SetServerGUID(serverGUID);
    }
}