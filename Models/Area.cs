#nullable disable
using SQLite;

namespace SmallBazar.Models;

[Table("Areas")]
public class Area 
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string Name { get; set; }
}