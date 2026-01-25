namespace Admins.Bans.Contract;

public enum BanType
{
    SteamID = 1,
    IP = 2
}

public interface IBan
{
    ulong Id { get; set; }
    ulong SteamId64 { get; set; }
    string PlayerName { get; set; }
    string PlayerIp { get; set; }
    BanType BanType { get; set; }
    ulong ExpiresAt { get; set; }
    ulong Length { get; set; }
    string Reason { get; set; }
    ulong AdminSteamId64 { get; set; }
    string AdminName { get; set; }
    string Server { get; set; }
    bool GlobalBan { get; set; }
    ulong CreatedAt { get; set; }
    ulong UpdatedAt { get; set; }
}