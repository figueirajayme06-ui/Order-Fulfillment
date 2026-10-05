using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Data.Database;

public partial class Line : IActivatable
{
    [NotMapped]
    public bool IsSubline => AgreementLineNumber?.Contains(".") == true;

    public Line ShallowCopy()
    {
        var line = (Line)this.MemberwiseClone();

        line.Id = 0;
        line.HeaderId = null;
        line.Header = null;

        return line;
    }
}