namespace Crypota;

public static class FileUtility
{
    public static byte[] GetFileInBytes(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath), "It is not a valid file path");
        }
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("You opened wrong door (file)", filePath);
        }
        
        byte[] fileBytes = File.ReadAllBytes(filePath);
        
        return fileBytes;
    }
    

    public static void WriteBytesToFile(string filePath, byte[] dataToWrite)
    {
        if (dataToWrite == null)
        {
            throw new ArgumentNullException(nameof(dataToWrite), "Got null data to write");
        }

        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrEmpty(filePath))
        { 
            throw new ArgumentNullException(nameof(filePath), "Path was null or empty");
        }
        
        
        File.WriteAllBytes(filePath, dataToWrite);
    }
    
    public static string? AddPrefixBeforeExtension(string? originalFilePath, string prefixToAdd)
    {
        if (string.IsNullOrEmpty(originalFilePath))
        {
            return originalFilePath;
        }

        string? directory = Path.GetDirectoryName(originalFilePath);
            
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFilePath);
        string extension = Path.GetExtension(originalFilePath);
            
        string newFileName = $"{fileNameWithoutExtension}{prefixToAdd ?? ""}{extension}";
        string newFilePath = Path.Combine(directory ?? "", newFileName);

        return newFilePath;
    }
    
}