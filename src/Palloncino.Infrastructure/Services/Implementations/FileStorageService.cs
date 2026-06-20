namespace Palloncino.Services.Implementations;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(IFormFile file, string subDirectory);
    Task<bool> DeleteFileAsync(string fileUrl);
}


public class FileStorageService(IWebHostEnvironment environment) : IFileStorageService
{
    public async Task<string> UploadFileAsync(IFormFile file, string subDirectory)
    {
        var uploadsFolder = Path.Combine(environment.WebRootPath ?? "wwwroot", "uploads", subDirectory);
        
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);
        
        var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        
        return $"/uploads/{subDirectory}/{uniqueFileName}";
    }
    
    public Task<bool> DeleteFileAsync(string fileUrl)
    {
        var filePath = Path.Combine(environment.WebRootPath ?? "wwwroot", fileUrl.TrimStart('/'));
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }
}