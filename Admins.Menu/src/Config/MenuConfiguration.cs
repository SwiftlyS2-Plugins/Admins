using Admins.Menu.Contract;
using SwiftlyS2.Shared.Natives;

namespace Admins.Menu;

public class CoreMenuConfiguration : IMenuConfiguration
{
    /// <inheritdoc/>
    public Color MenuColor { get; set; } = Color.FromHex("#00FEED");
}