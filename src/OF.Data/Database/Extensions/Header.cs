namespace OF.Data.Database;

public partial class Header : IActivatable
{
    public bool IsActivated
    {
        get
        {
            if (AgreementNumber?.ToLower().StartsWith("a") == true || ActivationStatus == 3)
            {
                return true;
            }

            return false;
        }
    }

    public ChangeOrder? CurrentChangeOrder => ChangeOrders?.FirstOrDefault(c => c.IsActive);
}