using Admins.API;
using Admins.Database.Groups;
using Dommel;
using SwiftlyS2.Shared;

namespace Admins.Groups;

public partial class Groups
{
    public static List<Group> AllGroups { get; private set; } = new();

    public static void Load()
    {
        Task.Run(() =>
        {
            foreach (var (player, admin) in ServerAdmins.ServerAdmins.PlayerAdmins)
            {
                if (!admin.Servers.Contains(Admins.ServerGUID)) continue;
                ServerAdmins.ServerAdmins.UnassignAdmin(player, admin);
            }

            var database = Admins.SwiftlyCore.Database.GetConnection("admins");
            AllGroups = [.. database.GetAll<Group>()];

            ServerAdmins.ServerAdmins.Load();
        });
    }
}