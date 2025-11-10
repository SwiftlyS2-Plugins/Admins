namespace Admins.Contract;

public interface IBan
{
    ulong Id { get; set; }
    ulong SteamId64 { get; set; }
    string PlayerName { get; set; }
    string PlayerIp { get; set; }
    int BanType { get; set; }
    ulong ExpiresAt { get; set; }
    ulong Length { get; set; }
    string Reason { get; set; }
    ulong AdminSteamId64 { get; set; }
    string AdminName { get; set; }
    string Server { get; set; }
    bool GlobalBan { get; set; }
}