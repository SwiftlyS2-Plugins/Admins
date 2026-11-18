namespace Admins.Contract;

public interface IGroup
{
    /// <summary>
    /// The unique identifier of the group.
    /// </summary>
    long Id { get; set; }

    /// <summary>
    /// The name of the group. Matched case-insensitively.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// The immunity level of the group.
    /// </summary>
    int Immunity { get; set; }

    /// <summary>
    /// The permissions granted by the group.
    /// </summary>
    List<string> Permissions { get; set; }

    /// <summary>
    /// The GUIDs of the servers the group applies to.
    /// </summary>
    List<string> Servers { get; set; }
}
