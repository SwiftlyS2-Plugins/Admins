using Admins.Comms.Contract;
using Admins.Comms.Database.Models;
using Dommel;
using SwiftlyS2.Shared;

namespace Admins.Comms.Manager;

public class CommsManager : ICommsManager
{
    private ISwiftlyCore Core = null!;
    private ServerComms _serverComms = null!;
    private Core.Contract.IConfigurationManager _configurationManager = null!;

    public event Action<ISanction>? OnAdminSanctionAdded;
    public event Action<ISanction>? OnAdminSanctionUpdated;
    public event Action<ISanction>? OnAdminSanctionRemoved;

    public CommsManager(ISwiftlyCore core, ServerComms serverComms)
    {
        Core = core;
        _serverComms = serverComms;
    }

    public void SetConfigurationManager(Core.Contract.IConfigurationManager configurationManager)
    {
        _configurationManager = configurationManager;
    }

    public void AddSanction(ISanction sanction)
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                sanction.Id = (ulong)(long)await db.InsertAsync((Sanction)sanction);
            }

            ServerComms.AllSanctions.TryAdd(sanction.Id, sanction);
            OnAdminSanctionAdded?.Invoke(sanction);
        });
    }

    public void ClearSanctions()
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.DeleteAllAsync<Sanction>();
            }

            ServerComms.AllSanctions.Clear();
        });
    }

    public ISanction? FindActiveSanction(ulong steamId64, string playerIp, SanctionKind sanctionKind)
    {
        return _serverComms.FindActiveSanction(steamId64, playerIp, sanctionKind);
    }

    public List<ISanction> GetSanctions()
    {
        return ServerComms.AllSanctions.Values.ToList();
    }

    public void RemoveSanction(ISanction sanction)
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.DeleteAsync((Sanction)sanction);
            }

            ServerComms.AllSanctions.TryRemove(sanction.Id, out _);
            OnAdminSanctionRemoved?.Invoke(sanction);
        });
    }

    public void SetSanctions(List<ISanction> sanctions)
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.DeleteAllAsync<Sanction>();
                await db.InsertAsync(sanctions.Select(s => (Sanction)s).ToList());
            }

            ServerComms.AllSanctions.Clear();
            foreach (var sanction in sanctions)
            {
                ServerComms.AllSanctions.TryAdd(sanction.Id, sanction);
            }
        });
    }

    public void UpdateSanction(ISanction sanction)
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.UpdateAsync((Sanction)sanction);
            }

            ServerComms.AllSanctions.AddOrUpdate(sanction.Id, sanction, (key, oldValue) => sanction);
            OnAdminSanctionUpdated?.Invoke(sanction);
        });
    }
}