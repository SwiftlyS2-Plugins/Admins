using SwiftlyS2.Shared.Natives;

namespace Admins.Menu.Contract;

public interface IMenuConfiguration
{
    /// <summary>
    /// The color of the admin menu.
    /// </summary>
    public Color MenuColor { get; set; }
}