#nullable disable
using SQLite;

namespace SmallBazar.Models;

[Table("Categories")]
public partial class Category : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    private string name;

    [Unique]
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }
}