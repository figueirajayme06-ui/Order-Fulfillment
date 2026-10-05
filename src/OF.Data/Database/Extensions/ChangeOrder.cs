namespace OF.Data.Database;

public partial class ChangeOrder
{
    public bool IsActive
    {
        get
        {
            switch (Status) {
                case (int)ChangeStatus.Completed:
                case (int)ChangeStatus.Rejected:
                    return false;
                default:
                    return true;
            }
        }
    }

    public bool IsEditable
    {
        get
        {
            switch (Status)
            {
                case (int)ChangeStatus.Pending:
                    return true;
                default:
                    return false;
            }
        }
    }
}