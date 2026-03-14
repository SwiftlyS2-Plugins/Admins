namespace Admins.Core.Contract;

public interface IGroup
{
    /// <summary>
    /// The unique identifier of the group.
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// The name of the group.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// The immunity level of the group.
    /// </summary>
    public int Immunity { get; set; }
    /// <summary>
    /// The permissions assigned to the group.
    /// </summary>
    public List<string> Permissions { get; set; }
    /// <summary>
    /// The servers the group has access to.
    /// </summary>
    public List<string> Servers { get; set; }
}