#nullable disable

using SQLite;

namespace SmallBazar.Models
{
    [Table("BoxCats")]
    public class BoxCat
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int BoxId { get; set; }

        public int CatId { get; set; }
    }

}
