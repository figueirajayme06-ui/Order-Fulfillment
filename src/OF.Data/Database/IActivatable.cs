namespace OF.Data.Database
{
    public interface IActivatable
    {
        int Id { get; set; }

        string? ActivationErrors { get; set; }

        int ActivationStatus { get; set; }

        string? ActivationInstanceId { get; set; }
    }
}
