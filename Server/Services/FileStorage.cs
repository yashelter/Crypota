using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Server.Services;

public class StoredFile
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string FilePath { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

public interface IFileStorage
{
    Task AddAsync(string filePath);
    Task<bool> ExistsAsync(string filePath);
    Task RemoveAsync(string filePath);
    
    public string StorageDir { get; }
}


[Obsolete("Было прикольно, но оказалось не нужно")]
public class FileStorageObsolete : BackgroundService, IFileStorage
{
    private readonly ILogger<FileStorageObsolete> _logger;
    private readonly IMongoCollection<StoredFile> _collection;
    private readonly TimeSpan _retention;
    private readonly string _storageDir;
    
    public string StorageDir => _storageDir;

    public FileStorageObsolete(IConfiguration config, ILogger<FileStorageObsolete> logger, IMongoClient mongoClient)
    {
        _logger = logger;
        var dbName = config["Mongo:Database"];
        var collName = "files";
        var db = mongoClient.GetDatabase(dbName);
        _collection = db.GetCollection<StoredFile>(collName);

        _retention = TimeSpan.FromDays(config.GetValue<long>("FileStorage:RetentionPeriodDays"));
        _storageDir = config["FileStorage:StorageDirectory"];
        Directory.CreateDirectory(_storageDir);
    }

    public async Task AddAsync(string filePath)
    {
        var record = new StoredFile
        {
            FilePath = filePath,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_retention)
        };
        await _collection.InsertOneAsync(record);
    }

    
    public async Task<bool> ExistsAsync(string filePath)
    {
        var filter = Builders<StoredFile>.Filter.Eq(f => f.FilePath, filePath);
        var record = await _collection.Find(filter).FirstOrDefaultAsync();
        if (record == null) return false;

        var fullPath = Path.Combine(_storageDir, filePath);
        return File.Exists(fullPath);
    }

    public async Task RemoveAsync(string filePath)
    {
        var filter = Builders<StoredFile>.Filter.Eq(f => f.FilePath, filePath);
        await _collection.DeleteOneAsync(filter);

        var fullPath = Path.Combine(_storageDir, filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var filter = Builders<StoredFile>.Filter.Lt(f => f.ExpiresAt, now);
                var expired = await _collection.Find(filter).ToListAsync(stoppingToken);

                foreach (var rec in expired)
                {
                    var fullPath = Path.Combine(_storageDir, rec.FilePath);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                    await _collection.DeleteOneAsync(
                        Builders<StoredFile>.Filter.Eq(f => f.Id, rec.Id),
                        cancellationToken: stoppingToken);
                    _logger.LogInformation("Deleted expired file {File}", rec.FilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during expired file cleanup");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}