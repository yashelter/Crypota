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

// Subscriber record
public class Subscriber
{
    public IServerStreamWriter<Message> MsgWriter { get; }
    public string Peer { get; }
    public Subscriber(IServerStreamWriter<Message> writer, string peer)
    {
        MsgWriter = writer; Peer = peer;
    }
}

// Exchange context for Diffie-Hellman
public class DhContext
{
    public ExchangeData Data { get; set; }
    public IServerStreamWriter<ExchangeData> Stream { get; set; }
    public string Peer { get; set; }
}

public class HackingGateService : HackingGate.HackingGateBase
{
    private readonly ILogger<HackingGateService> _logger;
    private readonly IMongoCollection<Room> _rooms;
    private readonly IFileStorage _fileStorage;
    private readonly IConstraint _constraint;

    // message subscribers
    private readonly ConcurrentDictionary<string, ConcurrentBag<Subscriber>> _subscribers =
        new ConcurrentDictionary<string, ConcurrentBag<Subscriber>>();

    // DH exchange contexts
    private readonly ConcurrentDictionary<string, List<DhContext>> _dhContexts =
        new ConcurrentDictionary<string, List<DhContext>>();

    public HackingGateService(IConfiguration config, ILogger<HackingGateService> logger, 
        IFileStorage fileStorage, IConstraint constraint)
    {
        var settings = config.GetSection("Mongo").Get<MongoSettings>();
        if (settings == null) throw new ArgumentNullException(nameof(config));
        var client = new MongoClient(settings.ConnectionString);
        var db = client.GetDatabase(settings.Database);
        _rooms = db.GetCollection<Room>(settings.RoomsCollection);
        _logger = logger;
        _fileStorage = fileStorage;
        _constraint = constraint;
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
        _subscribers.TryRemove(request.ChatId, out _);
        _dhContexts.TryRemove(request.ChatId, out _);
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

        if (_subscribers.TryGetValue(request.ChatId, out var bag))
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
        return Task.FromResult(result);
    }


    // Unary request, server streaming DiffieHellmanData
    public override async Task ExchangeDhParameters(ExchangeData request, IServerStreamWriter<ExchangeData> responseStream, ServerCallContext context)
    {
        var list = _dhContexts.GetOrAdd(request.ChatId, _ => new List<DhContext>());
        lock (list)
        {
            if (list.Count >= 2)
                throw new RpcException(new Status(StatusCode.ResourceExhausted, "Already two participants in DH exchange"));
            list.Add(new DhContext { Data = request, Stream = responseStream, Peer = context.Peer });
        }

        while (true)
        {
            if (context.CancellationToken.IsCancellationRequested) break;
            if (list.Count == 2)
            {
                foreach (var ctx in list)
                {
                    var mate = list.Find(x => x.Peer != ctx.Peer);
                    if (mate == null)
                    {
                        throw new RpcException(new Status(StatusCode.NotFound, "Not found data or was cancelled"));
                    }
                    await ctx.Stream.WriteAsync(mate.Data);
                }
                break;
            }
            await Task.Delay(100);
        }
    }

    public override async Task<MessageAck> SendMessage(Message request, ServerCallContext context)
    {
        if (!_subscribers.TryGetValue(request.ChatId, out var bag) || bag.Count < 1)
            return new MessageAck { Ok = false, Error = "No subscribers" };

        foreach (var sub in bag)
            if (sub.Peer != context.Peer)
                await sub.MsgWriter.WriteAsync(request);

        return new MessageAck { Ok = true };
    }

    public override async Task ReceiveMessages(RoomPassKey request, IServerStreamWriter<Message> responseStream, ServerCallContext context)
    {
        var bag = _subscribers.GetOrAdd(request.ChatId, _ => new ConcurrentBag<Subscriber>());
        if (bag.Count >= 2)
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "Room already has two participants"));

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