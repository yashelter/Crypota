using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Server.Models;
using StainsGate;

namespace Server.Services;

// MongoDB settings
public class MongoSettings
{
    public string ConnectionString { get; set; }
    public string Database { get; set; }
    public string RoomsCollection { get; set; }
}

// Room entity
public class Room
{
    [BsonId]
    public ObjectId Id { get; set; }
    public string ChatId { get; set; }
    public string OwnerUsername { get; set; }
    public DateTime CreationTime { get; set; }
    public int ParticipantCount { get; set; }
    public EncryptAlgo algo { get; set; }
    public EncryptMode cipherMode { get; set; }
    public PaddingMode paddingMode { get; set; }
}

public class Subscriber
{
    public IServerStreamWriter<Message> MsgWriter { get; }
    public string Peer { get; }
    public Subscriber(IServerStreamWriter<Message> writer, string peer)
    {
        MsgWriter = writer; Peer = peer;
    }
}

public sealed class Subscribers
{
    public readonly ConcurrentDictionary<string, ConcurrentBag<Subscriber>> Data =
        new ConcurrentDictionary<string, ConcurrentBag<Subscriber>>();
}


public class PendingDh
{
    public ExchangeData Request { get; set; }
    public IServerStreamWriter<ExchangeData> ResponseStream { get; set; }
    public TaskCompletionSource<bool> Tcs { get; } 
        = new(TaskCreationOptions.RunContinuationsAsynchronously);
}


public sealed class DhStateStore
{
    public readonly ConcurrentDictionary<string, PendingDh> Data 
        = new ConcurrentDictionary<string, PendingDh>();
}



public class HackingGateService : HackingGate.HackingGateBase
{
    private readonly ILogger<HackingGateService> _logger;
    private readonly IMongoCollection<Room> _rooms;
    private readonly IFileStorage _fileStorage;
    private readonly IConstraint _constraint;

    private readonly Subscribers _subscribers;
    private readonly DhStateStore _pendingDh;


    public HackingGateService(IConfiguration config, ILogger<HackingGateService> logger, 
        IFileStorage fileStorage, IConstraint constraint, Subscribers subscribers, DhStateStore pendingDh)
    {
        var settings = config.GetSection("Mongo").Get<MongoSettings>();
        if (settings == null) throw new ArgumentNullException(nameof(config));
        var client = new MongoClient(settings.ConnectionString);
        var db = client.GetDatabase(settings.Database);
        _rooms = db.GetCollection<Room>(settings.RoomsCollection);
        _logger = logger;
        _fileStorage = fileStorage;
        _constraint = constraint;
        _subscribers = subscribers;
        _pendingDh = pendingDh;
    }

    public override async Task<RoomPassKey> CreateRoom(RoomData request, ServerCallContext context)
    {
        _logger.LogInformation("Creating room  {0}, {1}, {2}", request.Algo.ToString(), request.CipherMode.ToString(), request.Padding.ToString());
        if (request == null) throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid room data"));
        var room = new Room
        {
            ChatId = Guid.NewGuid().ToString(),
            OwnerUsername = context.Peer,
            CreationTime = DateTime.UtcNow,
            ParticipantCount = 0,
            algo = request.Algo,
            cipherMode = request.CipherMode,
            paddingMode = request.Padding,
        };
        await _rooms.InsertOneAsync(room);
        return new RoomPassKey { ChatId = room.ChatId };
    }

    public override async Task<Empty> CloseRoom(RoomPassKey request, ServerCallContext context)
    {
        _logger.LogInformation("Closing room {0}", request.ChatId);

        await _rooms.DeleteOneAsync(r => r.ChatId == request.ChatId);
        _subscribers.Data.TryRemove(request.ChatId, out _);
        _pendingDh.Data.TryRemove(request.ChatId, out _);
        return new Empty();
    }

     public override async Task<RoomInfo> JoinRoom(RoomPassKey request, ServerCallContext context)
    {

        var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync();

        if (room == null)
        {
            _logger.LogWarning("JoinRoom: Room not found with ChatId {0}", request.ChatId);
            throw new RpcException(new Status(StatusCode.NotFound, "Room not found"));
        }
        
        var update = Builders<Room>.Update.Inc(r => r.ParticipantCount, 1);
        var updateResult = await _rooms.UpdateOneAsync(r => r.ChatId == request.ChatId, update);


        if (updateResult.ModifiedCount == 0)
        {
            _logger.LogWarning("JoinRoom: Failed to increment participant count for ChatId {ChatId}, though room was initially found.", request.ChatId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to update room participant count."));
        }
        
        _logger.LogDebug("Joined to room {0}", request.ChatId);

        return new RoomInfo
        {
            ChatId = room.ChatId,
            OwnerUsername = room.OwnerUsername,
            CreationTime = room.CreationTime.ToString("o"),
            Settings = new RoomData
            {
                Algo = room.algo,
                CipherMode = room.cipherMode,
                Padding = room.paddingMode
            }
        };
    }

    public override async Task<Empty> LeaveRoom(RoomPassKey request, ServerCallContext context)
    {
        var update = Builders<Room>.Update.Inc(r => r.ParticipantCount, -1);
        await _rooms.UpdateOneAsync(r => r.ChatId == request.ChatId, update);

        if (_subscribers.Data.TryGetValue(request.ChatId, out var bag))
        {
            var sysMsg = new Message { ChatId = request.ChatId, Data = ByteString.CopyFromUtf8("A user has left the room") };
            foreach (var sub in bag)
                if (sub.Peer != context.Peer)
                    await sub.MsgWriter.WriteAsync(sysMsg);
        }
        return new Empty();
    }

    public override Task<DiffieHellmanData> GetPublicDhParameters(DiffieHellmanQuery request, ServerCallContext context)
    {
        var g = ByteString.CopyFrom(_constraint.GetPublicG(request.ChatId));
        var p = ByteString.CopyFrom(_constraint.GetPublicP(request.ChatId));

        var result = new DiffieHellmanData()
        {
            GValue = g,
            PValue = p
        };
        _logger.LogDebug("Gave public values");

        return Task.FromResult(result);
    }


    public override async Task ExchangeDhParameters(ExchangeData request, IServerStreamWriter<ExchangeData> responseStream, ServerCallContext context)
    {
        _logger.LogDebug("Started exchange with DhParameters");

        string chatId = request.ChatId;

        var pending = new PendingDh { Request = request, ResponseStream = responseStream };
        if (_pendingDh.Data.TryAdd(chatId, pending))
        {
            try
            {
                await pending.Tcs.Task;
            }
            catch (OperationCanceledException) { /* клиент отключился */ }
            return;
        }
        else
        {
            if (_pendingDh.Data.TryRemove(chatId, out var first))
            {
                await first.ResponseStream.WriteAsync(request);
                await responseStream.WriteAsync(first.Request);
                first.Tcs.SetResult(true);
            }
            else
            {
                throw new RpcException(
                    new Status(StatusCode.Internal, "Ошибка синхронизации DH"));
            }
        }
        _logger.LogDebug("Exchanged with DhParameters");
    }

    public override async Task<MessageAck> SendMessage(Message request, ServerCallContext context)
    {
        _logger.LogDebug("{0}: chat:{1}, message:{2}, sender:{3}", nameof(SendMessage), request.ChatId, request.Data, request.FromUsername);
        if (!_subscribers.Data.TryGetValue(request.ChatId, out var bag) || bag.Count < 1)
            return new MessageAck { Ok = false, Error = "No subscribers" };

        foreach (var sub in bag)
        {
            if (sub.Peer != context.Peer)
            {
                await sub.MsgWriter.WriteAsync(request);
                _logger.LogDebug("Sended message {0}, {1}, {2}", sub.Peer, context.Peer, request.Data);

            }
            else
            {
                _logger.LogDebug("Self message {0}, {1}, total: {2}", sub.Peer, context.Peer, bag.Count);
            }
        }

        return new MessageAck { Ok = true };
    }

    public override async Task ReceiveMessages(RoomPassKey request, IServerStreamWriter<Message> responseStream, ServerCallContext context)
    {
        // TODO: possibly something with login
        var bag = _subscribers.Data.GetOrAdd(request.ChatId, _ => new ConcurrentBag<Subscriber>());
        if (bag.Count >= 2)
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "Room already has two participants"));

        _logger.LogDebug("To room: {0} added new participant", request.ChatId);

        bag.Add(new Subscriber(responseStream, context.Peer));

        try { await Task.Delay(-1, context.CancellationToken); }
        catch (TaskCanceledException) { /* client disconnected */ }
    }

    public override async Task<FileAck> SendFile(IAsyncStreamReader<FileChunk> requestStream, ServerCallContext context)
    {
        long total = 0;
        EncryptedFile file = null!;
        bool initialized = false;
        string path = "";
        await foreach (var chunk in requestStream.ReadAllAsync())
        {
            if (!initialized)
            {
                Directory.CreateDirectory(_fileStorage.StorageDir);

                path = Path.Combine(_fileStorage.StorageDir, $"{chunk.ChatId}.{chunk.FileName}.enc");
                file = new EncryptedFile(path);
                initialized = true;
            }

            total += chunk.Data.Length;
            await file.AppendFragmentAtOffsetAsync(chunk.Offset, chunk.Data.ToByteArray());
        }

        if (initialized)
        {
            await _fileStorage.AddAsync(Path.GetFileName(path));
            file.Dispose();
        }

        return new FileAck { Ok = true, TotalSize = total };
    }


    public override async Task ReceiveFile(FileRequest request, IServerStreamWriter<FileChunk> responseStream, ServerCallContext context)
    {
        var fullPath = Path.Combine(_fileStorage.StorageDir, $"{request.ChatId}.{request.FileName}.enc");
        
        bool isExists = await _fileStorage.ExistsAsync(fullPath);
        if (!isExists)
            throw new RpcException(new Status(StatusCode.NotFound, "File not found"));

        using var file = new EncryptedFile(fullPath);

        while (true)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            var fragment = await file.ReadNextFragmentAsync();
            if (fragment == null)
                break;

            var (data, offset) = fragment.Value;
            var chunk = new FileChunk
            {
                ChatId  = request.ChatId,
                FileName = request.FileName,
                Data     = ByteString.CopyFrom(data),
                Offset   = offset
            };
            await responseStream.WriteAsync(chunk);
        }
    }
}