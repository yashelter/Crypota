using System.Collections.Concurrent;
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
        MsgWriter = writer;
        Peer = peer;
    }
}

// Represents a two-way chat session with cancellation support
public class ChatSession
{
    public List<Subscriber> Subscribers { get; } = new (2);
    public readonly CancellationTokenSource SessionCts = new CancellationTokenSource();
}

public sealed class SessionStore
{
    public ConcurrentDictionary<string, ChatSession> Data { get; } = new();
}

public class PendingDh
{
    public required ExchangeData Request { get; init; }
    public required IServerStreamWriter<ExchangeData> ResponseStream { get; init; }
    public TaskCompletionSource<bool> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationTokenSource Cts { get; } = new();
}

public sealed class DhStateStore
{
    public ConcurrentDictionary<string, PendingDh> Data { get; } = new ();
}

public class HackingGateService : HackingGate.HackingGateBase
{
    private readonly ILogger<HackingGateService> _logger;
    private readonly IMongoCollection<Room> _rooms;
    private readonly IFileStorage _fileStorage;
    private readonly IConstraint _constraint;
    private readonly SessionStore _sessions;
    private readonly DhStateStore _pendingDh;

    public HackingGateService(
        IConfiguration config,
        ILogger<HackingGateService> logger,
        IFileStorage fileStorage,
        IConstraint constraint,
        SessionStore sessions,
        DhStateStore pendingDh)
    {
        var settings = config.GetSection("Mongo").Get<MongoSettings>();
        if (settings == null) throw new ArgumentNullException(nameof(config));
        var client = new MongoClient(settings.ConnectionString);
        var db = client.GetDatabase(settings.Database);
        _rooms = db.GetCollection<Room>(settings.RoomsCollection);
        _logger = logger;
        _fileStorage = fileStorage;
        _constraint = constraint;
        _sessions = sessions;
        _pendingDh = pendingDh;
    }

    public override async Task<RoomPassKey> CreateRoom(RoomData request, ServerCallContext context)
    {
        _logger.LogInformation("[CreateRoom] Algo={Algo}, Mode={Mode}, Padding={Padding}", request.Algo, request.CipherMode, request.Padding);
        if (request == null)
        {
            _logger.LogError("[CreateRoom] Invalid room data");
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid room data"));
        }
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
        _logger.LogInformation("[CreateRoom] Created room {ChatId}", room.ChatId);
        return new RoomPassKey { ChatId = room.ChatId };
    }

    public override async Task<Empty> CloseRoom(RoomPassKey request, ServerCallContext context)
    {
        _logger.LogInformation("[CloseRoom] Closing room {ChatId}", request.ChatId);
        await _rooms.DeleteOneAsync(r => r.ChatId == request.ChatId);

        if (_sessions.Data.TryRemove(request.ChatId, out var session))
        {
            session.SessionCts.Cancel();
            _logger.LogInformation("[CloseRoom] Session cancelled for {ChatId}", request.ChatId);
        }
        _pendingDh.Data.TryRemove(request.ChatId, out _);
        return new Empty();
    }

    public override async Task<RoomInfo> JoinRoom(RoomPassKey request, ServerCallContext context)
    {
        _logger.LogInformation("[JoinRoom] Peer={Peer}, ChatId={ChatId}", context.Peer, request.ChatId);
        var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync();
        if (room == null)
        {
            _logger.LogWarning("[JoinRoom] Room not found: {ChatId}", request.ChatId);
            throw new RpcException(new Status(StatusCode.NotFound, "Room not found"));
        }
        var update = Builders<Room>.Update.Inc(r => r.ParticipantCount, 1);
        var result = await _rooms.UpdateOneAsync(r => r.ChatId == request.ChatId, update);
        if (result.ModifiedCount == 0)
        {
            _logger.LogError("[JoinRoom] Failed to increment participant count for {ChatId}", request.ChatId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to update participant count"));
        }
        _logger.LogInformation("[JoinRoom] Joined room {ChatId}", request.ChatId);
        return new RoomInfo
        {
            ChatId = room.ChatId,
            OwnerUsername = room.OwnerUsername,
            CreationTime = room.CreationTime.ToString("o"),
            Settings = new RoomData { Algo = room.algo, CipherMode = room.cipherMode, Padding = room.paddingMode }
        };
    }

    public override async Task<Empty> LeaveRoom(RoomPassKey request, ServerCallContext context)
    {
        _logger.LogInformation("[LeaveRoom] Peer={Peer} leaving ChatId={ChatId}", context.Peer, request.ChatId);
        var update = Builders<Room>.Update.Inc(r => r.ParticipantCount, -1);
        await _rooms.UpdateOneAsync(r => r.ChatId == request.ChatId, update);

        if (_sessions.Data.TryRemove(request.ChatId, out var session))
        {
            session.SessionCts.Cancel();
            _logger.LogInformation("[LeaveRoom] Session cancelled for ChatId={ChatId}", request.ChatId);
        }
        return new Empty();
    }

    public override Task<DiffieHellmanData> GetPublicDhParameters(DiffieHellmanQuery request, ServerCallContext context)
    {
        _logger.LogDebug("[GetPublicDhParameters] ChatId={ChatId}", request.ChatId);
        var g = ByteString.CopyFrom(_constraint.GetPublicG(request.ChatId));
        var p = ByteString.CopyFrom(_constraint.GetPublicP(request.ChatId));
        return Task.FromResult(new DiffieHellmanData { GValue = g, PValue = p });
    }

    public override async Task ExchangeDhParameters(ExchangeData request, IServerStreamWriter<ExchangeData> responseStream, ServerCallContext context)
    {
        var chatId = request.ChatId;
        _logger.LogDebug("[ExchangeDh] Start DH for ChatId={ChatId}, Peer={Peer}", chatId, context.Peer);

        var pending = new PendingDh { Request = request, ResponseStream = responseStream };

        using var reg = context.CancellationToken.Register(() =>
        {
            pending.Cts.Cancel();
            pending.Tcs.TrySetCanceled();
            _logger.LogWarning("[ExchangeDh] DH cancelled for ChatId={ChatId}, Peer={Peer}", chatId, context.Peer);
            _pendingDh.Data.TryRemove(chatId, out _);
        });

        if (_pendingDh.Data.TryAdd(chatId, pending))
        {
            // First participant: wait for partner
            _logger.LogInformation("[ExchangeDh] Waiting for second peer for ChatId={ChatId}", chatId);
            try
            {
                await pending.Tcs.Task.ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogInformation("[ExchangeDh] First peer DH cancelled for ChatId={ChatId}", chatId);
                return;
            }
            finally
            {
                _pendingDh.Data.TryRemove(chatId, out _);
            }
        }
        else
        {
            // Second participant: perform exchange
            if (_pendingDh.Data.TryRemove(chatId, out var first))
            {
                _logger.LogInformation("[ExchangeDh] Exchanging parameters between peers for ChatId={ChatId}", chatId);
                try
                {
                    // Send second's data to first
                    await first.ResponseStream.WriteAsync(request).ConfigureAwait(false);
                    _logger.LogDebug("[ExchangeDh] Sent second peer data to first for ChatId={ChatId}", chatId);

                    // Send first's data to second
                    await responseStream.WriteAsync(first.Request).ConfigureAwait(false);
                    _logger.LogDebug("[ExchangeDh] Sent first peer data to second for ChatId={ChatId}", chatId);

                    first.Tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ExchangeDh] Error during DH exchange for ChatId={ChatId}", chatId);
                    first.Tcs.TrySetException(ex);
                    throw new RpcException(new Status(StatusCode.Internal, "DH exchange failed"));
                }
            }
            else
            {
                _logger.LogError("[ExchangeDh] Pending DH not found for ChatId={ChatId}", chatId);
                throw new RpcException(new Status(StatusCode.Internal, "DH state lost"));
            }
        }

        _logger.LogInformation("[ExchangeDh] Completed DH for ChatId={ChatId}", chatId);
    }

    public override async Task<MessageAck> SendMessage(Message request, ServerCallContext context)
    {
        _logger.LogDebug("[SendMessage] ChatId={ChatId}, From={From}", request.ChatId, request.FromUsername);
        if (!_sessions.Data.TryGetValue(request.ChatId, out var session) || session.Subscribers.Count == 0)
        {
            _logger.LogWarning("[SendMessage] No subscribers for ChatId={ChatId}", request.ChatId);
            return new MessageAck { Ok = false, Error = "No subscribers" };
        }
        foreach (var sub in session.Subscribers)
        {
            if (sub.Peer == context.Peer) continue;
            try
            {
                await sub.MsgWriter.WriteAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SendMessage] Error sending message to Peer={Peer}, ChatId={ChatId}", sub.Peer, request.ChatId);
                session.SessionCts.Cancel();
                return new MessageAck { Ok = false, Error = "Delivery failed" };
            }
        }
        return new MessageAck { Ok = true };
    }

    public override async Task ReceiveMessages(RoomPassKey request, IServerStreamWriter<Message> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("[ReceiveMessages] Peer={Peer} joining ChatId={ChatId}", context.Peer, request.ChatId);
        var session = _sessions.Data.GetOrAdd(request.ChatId, _ => new ChatSession());
        lock (session)
        {
            if (session.Subscribers.Count >= 2)
            {
                _logger.LogWarning("[ReceiveMessages] ChatId={ChatId} full, rejecting Peer={Peer}", request.ChatId, context.Peer);
                return;
            }
            session.Subscribers.Add(new Subscriber(responseStream, context.Peer));
        }

        using var reg = context.CancellationToken.Register(() =>
        {
            _logger.LogWarning("[ReceiveMessages] Peer disconnected Peer={Peer}, ChatId={ChatId}", context.Peer, request.ChatId);
            session.SessionCts.Cancel();
            _sessions.Data.TryRemove(request.ChatId, out _);
        });

        try
        {
            await Task.Delay(Timeout.Infinite, session.SessionCts.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("[ReceiveMessages] Session ended for ChatId={ChatId}", request.ChatId);
        }
    }

    public override async Task<FileAck> SendFile(IAsyncStreamReader<FileChunk> requestStream, ServerCallContext context)
    {
        _logger.LogInformation("[SendFile] Peer={Peer} starting file upload", context.Peer);
        long total = 0;
        EncryptedFile file = null!;
        bool initialized = false;
        string path = string.Empty;

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
            await file.AppendFragmentAtOffsetAsync(chunk.Offset, chunk.Data.ToByteArray()).ConfigureAwait(false);
        }
        if (initialized)
        {
            await _fileStorage.AddAsync(Path.GetFileName(path));
            file.Dispose();
            _logger.LogInformation("[SendFile] Completed upload ChatId={ChatId}, Size={Size}", path, total);
        }
        return new FileAck { Ok = true, TotalSize = total };
    }

    public override async Task ReceiveFile(FileRequest request, IServerStreamWriter<FileChunk> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("[ReceiveFile] Peer={Peer} requesting file {File}", context.Peer, request.FileName);
        var fullPath = Path.Combine(_fileStorage.StorageDir, $"{request.ChatId}.{request.FileName}.enc");
        if (!await _fileStorage.ExistsAsync(fullPath).ConfigureAwait(false))
        {
            _logger.LogWarning("[ReceiveFile] File not found {Path}", fullPath);
            throw new RpcException(new Status(StatusCode.NotFound, "File not found"));
        }
        using var file = new EncryptedFile(fullPath);
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var fragment = await file.ReadNextFragmentAsync().ConfigureAwait(false);
            if (fragment == null) break;
            var (data, offset) = fragment.Value;
            var chunk = new FileChunk { ChatId = request.ChatId, FileName = request.FileName, Data = ByteString.CopyFrom(data), Offset = offset };
            await responseStream.WriteAsync(chunk).ConfigureAwait(false);
        }
        _logger.LogInformation("[ReceiveFile] Completed streaming file {File}", request.FileName);
    }
}