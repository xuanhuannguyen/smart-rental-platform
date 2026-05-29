namespace SmartRentalPlatform.Contracts.Common;
public class ApiResponse<T> {
    public bool Success {get ; set ;}
    public string Message {get ; set ; } = "Ok";
    public T? Data { get ; set ;}
}
// Success Response
