#nullable disable
using SmallBazar.Utils;
using SQLite;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SmallBazar.Models;

[Table("Boxes")]
public class Box : INotifyPropertyChanged
{
    private int id;
    private int number;
    private string description;
    private int areaCode;
    private DateTime createdAt = DateTime.Now;

    [PrimaryKey, AutoIncrement]
    public int Id
    {
        get => id;
        set
        {
            if (id == value) return;
            id = value;
            OnPropertyChanged();
        }
    }

    [Unique]
    public int Number
    {
        get => number;
        set
        {
            if (number == value) return;
            number = value;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => description;
        set
        {
            if (description == value) return;
            description = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DescriptionForDisplay));
        }
    }

    public int? AreaCode
    {
        get => areaCode;
        set
        {
            if (areaCode == value) return;
            if (value != null)
            {
                areaCode = (int)value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Last modification date (was creation date but has been changed in v 1.5.5 with a change in Dataservice to stamp the record date)
    /// </summary>
    public DateTime CreatedAt
    {
        get => createdAt;
        set
        {
            if (createdAt != value)
            {
                createdAt = value;
                OnPropertyChanged();
            }
        }
    }

    public string DescriptionForDisplay
    {
        get
        {
            var sb = new StringBuilder(Description.Trim());
            // remove CR, LF, TAB
            sb.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            // remove duplicated spaces
            var result = Regex.Replace(sb.ToString(), @"\s+", " ");
            return result;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

