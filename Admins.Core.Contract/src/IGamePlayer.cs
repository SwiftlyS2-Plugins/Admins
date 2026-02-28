using Admins.Bans.Contract;
using Admins.Comms.Contract;

namespace Admins.Core.Contract;

public interface IGamePlayer
{
    void SetBansManager(IBansManager? bansManager);
    void SetCommsManager(ICommsManager? commsManager);
}
