namespace OF.UI.Models;

public interface IResponse
{
    bool IsSuccess { get; set; }

    string ErrorMessage { get; set; }
}
