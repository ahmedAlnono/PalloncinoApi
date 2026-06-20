namespace Palloncino.Services.Interfaces;

public class StoredResponse
{
    public string Key { get; set; } = "";
    public string RequestType { get; set; } = "";
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
