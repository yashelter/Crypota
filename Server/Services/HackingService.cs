using System.Collections.Concurrent;
using Google.Protobuf;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Server.Models;
using StainsGate;
using PaddingMode = StainsGate.PaddingMode;

namespace Server.Services;

// MongoDB settings
public class MongoSettings
{
    public string ConnectionString { get; set; } = null!;
    public string Database { get; set; } = null!;
    public string RoomsCollection { get; set; } = null!;
}

// Room entity
public class Room
{
    [BsonId]
    public ObjectId Id { get; set; }
    public string ChatId { get; set; } = null!;
    public string OwnerUsername { get; set; } = null!;
    public string? SubscriberUsername { get; set; }
    public DateTime CreationTime { get; set; }
    public int ParticipantCount { get; set; }
    public EncryptAlgo Algo { get; set; } 
    public EncryptMode CipherMode { get; set; }
    public PaddingMode PaddingMode { get; set; }
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

public class PendingFileTransfer
{
    public string ChatId { get; }
    public ByteString OriginalFileName { get; }
    public MessageType OriginalType { get; }
    public string SenderUsername { get; }

    public required FileChunk FirstChunk { get; init; }
    
    public required  IAsyncEnumerator<FileChunk> SenderStream { get; init; } 
    public TaskCompletionSource<long> UploadCompleteTcs { get; } 
    public CancellationTokenSource LifetimeCts { get; }

    public long TotalSize { get; set; } = 0;

    public PendingFileTransfer(string chatId, ByteString fileName, MessageType type, string senderUsername)
    {
        ChatId = chatId;
        OriginalFileName = fileName;
        OriginalType = type;
        SenderUsername = senderUsername;
        UploadCompleteTcs = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        LifetimeCts = new CancellationTokenSource();
    }
}

// Represents a two-way chat session with cancellation support
public class ChatSession
{
    public List<Subscriber> MessageSubscribers { get; } = new (2);
    
    public ConcurrentDictionary<string, PendingFileTransfer> FileTransfers { get; } = new();
    public readonly CancellationTokenSource SessionCts = new CancellationTokenSource();

}

public sealed class SessionStore
{
    public ConcurrentDictionary<string, ChatSession> ChatSessions { get; } = new();
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
    private readonly IConstraint _constraint;
    private readonly SessionStore _sessions;
    private readonly DhStateStore _pendingDh;

    public HackingGateService(
        IConfiguration config,
        ILogger<HackingGateService> logger,
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
        _constraint = constraint;
        _sessions = sessions;
        _pendingDh = pendingDh;
    }

    public override async Task<RoomPassKey> CreateRoom(RoomData request, ServerCallContext context)
    {
        _logger.LogInformation("[CreateRoom] Algo={Algo}, Mode={Mode}, Padding={Padding}", request.Algo,
            request.CipherMode, request.Padding);
        if (request == null)
        {
            _logger.LogError("[CreateRoom] Invalid room data");
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid room data"));
        }
        string username = context.UserState["username"] as string ?? throw new RpcException(
            new Status(StatusCode.Unauthenticated, "No JWT info inside"));
        
        var room = new Room
        {
            ChatId = Guid.NewGuid().ToString(),
            OwnerUsername = username,
            CreationTime = DateTime.UtcNow,
            ParticipantCount = 1,
            Algo = request.Algo,
            CipherMode = request.CipherMode,
            PaddingMode = request.Padding,
        };
        await _rooms.InsertOneAsync(room);
        _logger.LogInformation("[CreateRoom] Created room {ChatId}", room.ChatId);
        return new RoomPassKey { ChatId = room.ChatId };
    }
    

    public override async Task<RoomInfo> JoinRoom(RoomPassKey request, ServerCallContext context)
    {
        _logger.LogInformation("[JoinRoom] Peer={Peer}, ChatId={ChatId}", context.Peer, request.ChatId);
        string username = context.UserState["username"] as string ?? throw new RpcException(
            new Status(StatusCode.Unauthenticated, "No JWT info inside"));

        var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync();
        if (room == null)
        {
            _logger.LogWarning("[JoinRoom] Room not found: {ChatId}", request.ChatId);
            throw new RpcException(new Status(StatusCode.NotFound, "Room not found"));
        }

        if (room.ParticipantCount != 1)
        {
            _logger.LogWarning("[JoinRoom] Participant count mismatch: {ChatId}", request.ChatId);
            throw new RpcException(new Status(StatusCode.Unavailable, "Participant count mismatch"));
        }

        lock (_rooms)
        {
            var update = Builders<Room>.Update.Set(r => r.ParticipantCount, 2)
                .Set(r => r.SubscriberUsername, username);
            
            var result = _rooms.UpdateOne(r => r.ChatId == request.ChatId, update);
            
            if (result.ModifiedCount == 0)
            {
                _logger.LogError("[JoinRoom] Failed to increment participant count for {ChatId}", request.ChatId);
                throw new RpcException(new Status(StatusCode.Internal, "Failed to update participant count"));
            }
        }

        _logger.LogInformation("[JoinRoom] Joined room {ChatId}", request.ChatId);
        return new RoomInfo
        {
            ChatId = room.ChatId,
            OwnerUsername = room.OwnerUsername,
            CreationTime = room.CreationTime.ToString("o"),
            OtherSubscriber = room.SubscriberUsername ?? "Никто",
            Settings = new RoomData { Algo = room.Algo, CipherMode = room.CipherMode, Padding = room.PaddingMode }
        };
    }

    public override async Task<Empty> KickFromRoom(RoomPassKey request, ServerCallContext context)
    {
        _logger.LogInformation("[KickFromRoom] Peer={Peer} leaving ChatId={ChatId}", context.Peer, request.ChatId);
        string username = context.UserState["username"] as string ?? throw new RpcException(
            new Status(StatusCode.Unauthenticated, "No JWT info inside"));

        var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync();

        if (room.OwnerUsername != username)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Need to be owner of room for this action"));
        }

        lock (_rooms)
        {
            if (room.ParticipantCount == 2)
            {
                var update = Builders<Room>.Update.Set(r => r.ParticipantCount, 1)
                    .Set(r => r.SubscriberUsername, null);

                _rooms.UpdateOne(r => r.ChatId == request.ChatId, update);       
                _pendingDh.Data.TryRemove(request.ChatId, out _);
            }
        }

        if (_sessions.ChatSessions.TryRemove(request.ChatId, out var session))
        {
            await session.SessionCts.CancelAsync();
            _pendingDh.Data.TryRemove(request.ChatId, out _);
            _logger.LogInformation("[KickFromRoom] Session cancelled for ChatId={ChatId}", request.ChatId);
        }

        return new Empty();
    }

    public override async Task<Empty> CloseRoom(RoomPassKey request, ServerCallContext context)
    {
        _logger.LogInformation("[CloseRoom] Closing room {ChatId}", request.ChatId);
        return await LeaveRoom(request, context);
    }

    public override async Task<Empty> LeaveRoom(RoomPassKey request, ServerCallContext context)
    {
        _logger.LogInformation("[LeaveRoom] Peer={Peer} leaving ChatId={ChatId}", context.Peer, request.ChatId);
        string username = context.UserState["username"] as string ?? throw new RpcException(
            new Status(StatusCode.Unauthenticated, "No JWT info inside"));
        
        var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync();
        
        if (room.OwnerUsername == username)
        {
            await _rooms.DeleteOneAsync(r => r.ChatId == request.ChatId);
        }
        else
        {
            var update = Builders<Room>.Update.Set(r => r.ParticipantCount, 1).
                Set(r => r.SubscriberUsername, null);
            await _rooms.UpdateOneAsync(r => r.ChatId == request.ChatId, update);
        }

        if (_sessions.ChatSessions.TryRemove(request.ChatId, out var session))
        {
            await session.SessionCts.CancelAsync();
            _pendingDh.Data.TryRemove(request.ChatId, out _);
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

    public override async Task ExchangeDhParameters(ExchangeData request,
        IServerStreamWriter<ExchangeData> responseStream, ServerCallContext context)
    {
        var chatId = request.ChatId;
        
        string username = context.UserState["username"] as string ?? 
                          throw new RpcException(new Status(StatusCode.Unauthenticated, "No JWT info inside"));
        
        var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync();

        if (room == null || (room.SubscriberUsername != username && room.OwnerUsername != username))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not belongs to room to send one"));
        }
        
        _logger.LogDebug("[ExchangeDh] Start DH for ChatId={ChatId}, Peer={Peer}", chatId, context.Peer);

        var pending = new PendingDh { Request = request, ResponseStream = responseStream };

        await using var reg = context.CancellationToken.Register(() =>
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
            catch (TaskCanceledException)
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
                    await first.Cts.CancelAsync();
                    throw new RpcException(new Status(StatusCode.Internal, "DH exchange failed"));
                }
            }
            else
            {
                if (first is not null) await first.Cts.CancelAsync();
                _logger.LogError("[ExchangeDh] Pending DH not found for ChatId={ChatId}", chatId);
                throw new RpcException(new Status(StatusCode.Internal, "DH state lost"));
            }
        }

        _logger.LogInformation("[ExchangeDh] Completed DH for ChatId={ChatId}", chatId);
    }

    public override async Task<MessageAck> SendMessage(Message request, ServerCallContext context)
    {
        _logger.LogDebug("[SendMessage] ChatId={ChatId}, From={From}", request.ChatId, request.FromUsername);
        if (!_sessions.ChatSessions.TryGetValue(request.ChatId, out var session) || session.MessageSubscribers.Count == 0)
        {
            _logger.LogWarning("[SendMessage] No subscribers for ChatId={ChatId}", request.ChatId);
            return new MessageAck { Ok = false, Error = "No subscribers" };
        }
        string username = context.UserState["username"] as string ?? throw new RpcException(
            new Status(StatusCode.Unauthenticated, "No JWT info inside"));
        
        var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync();

        if (room == null || (room.SubscriberUsername != username && room.OwnerUsername != username))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not belongs to room to send one"));
        }


        foreach (var sub in session.MessageSubscribers)
        {
            if (sub.Peer == context.Peer) continue;
            try
            {
                await sub.MsgWriter.WriteAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SendMessage] Error sending message to Peer={Peer}, ChatId={ChatId}", sub.Peer,
                    request.ChatId);
                await session.SessionCts.CancelAsync();
                return new MessageAck { Ok = false, Error = "Delivery failed" };
            }
        }

        return new MessageAck { Ok = true };
    }

    public override async Task ReceiveMessages(RoomPassKey request, IServerStreamWriter<Message> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("[ReceiveMessages] Peer={Peer} joining ChatId={ChatId}", context.Peer, request.ChatId);
        var session = _sessions.ChatSessions.GetOrAdd(request.ChatId, _ => new ChatSession());
        
        string username = context.UserState["username"] as string ?? throw new RpcException(
            new Status(StatusCode.Unauthenticated, "No JWT info inside"));
        
        var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync();

        if (room == null || (room.SubscriberUsername != username && room.OwnerUsername != username))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not belongs to room"));
        }

        lock (session)
        {
            if (session.MessageSubscribers.Count >= 2)
            {
                _logger.LogWarning("[ReceiveMessages] ChatId={ChatId} full, rejecting Peer={Peer}", request.ChatId,
                    context.Peer);
                return;
            }

            session.MessageSubscribers.Add(new Subscriber(responseStream, context.Peer));
        }

        await using var reg = context.CancellationToken.Register(() =>
        {
            _logger.LogWarning("[ReceiveMessages] Peer disconnected Peer={Peer}, ChatId={ChatId}", context.Peer,
                request.ChatId);
            session.SessionCts.Cancel();
            _sessions.ChatSessions.TryRemove(request.ChatId, out _);
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
            
            var enumerator = requestStream.ReadAllAsync().GetAsyncEnumerator();

            if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                return new FileAck { Ok = false, Error = "Empty stream" };
            }
            
            FileChunk chunk = enumerator.Current;

            string chatId = chunk.ChatId;
            string filename = $"{chunk.ChatId}.{chunk.FileName.ToStringUtf8()}";
            string username = context.UserState["username"] as string ?? "Error while parsing name (JWT)";

            if (!_sessions.ChatSessions.TryGetValue(chatId, out var session) || session.MessageSubscribers.Count != 2)
            {
                return new FileAck { Ok = false, Error = "Not existing connection between users" };
            }
            var room = await _rooms.Find(r => r.ChatId == chatId).FirstOrDefaultAsync();

            if (room == null || (room.SubscriberUsername != username && room.OwnerUsername != username))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Not belongs to room"));
            }

            
            PendingFileTransfer fileTransfer;

            lock (session)
            {
                if (session.FileTransfers.ContainsKey(filename))
                {
                    return new FileAck { Ok = false, Error = "Already sending file with provided filename" };
                }
                fileTransfer = session.FileTransfers.GetOrAdd(filename, _ =>
                    new PendingFileTransfer(chunk.ChatId, chunk.FileName, chunk.Type, username)
                    {
                        FirstChunk = chunk,
                        SenderStream = enumerator
                    });
            }
            
            await using var reg = context.CancellationToken.Register(async void ( ) =>
            {
                try
                {
                    _logger.LogWarning("[SendFile] Peer disconnected Peer={Peer}, ChatId={ChatId}", context.Peer, chatId);
                    session.FileTransfers.TryRemove(filename, out _);
                    await fileTransfer.LifetimeCts.CancelAsync();
                    fileTransfer.UploadCompleteTcs.TrySetCanceled(fileTransfer.LifetimeCts.Token); 
                    await enumerator.DisposeAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[SendFile] Session ended for ChatId={ChatId}", chatId);
                }
            });

            try
            {
                foreach (var sub in session.MessageSubscribers)
                {
                    if (sub.Peer == context.Peer) continue;
                    try
                    {
                        await sub.MsgWriter.WriteAsync(new Message()
                        {
                            ChatId = chatId,
                            Data = chunk.FileName,
                            FromUsername = username,
                            Meta = chunk.Meta,
                            Type = chunk.Type,
                        }).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SendFile] Error sending message to Peer={Peer}, ChatId={ChatId}",
                            sub.Peer, chatId);
                        await session.SessionCts.CancelAsync();
                        return new FileAck { Ok = false, Error = "Delivery failed" };
                    }
                }

                long totalSizeFromReceiver = await fileTransfer.UploadCompleteTcs.Task
                    .WaitAsync(fileTransfer.LifetimeCts.Token)
                    .ConfigureAwait(false);

                await enumerator.DisposeAsync();
                session.FileTransfers.TryRemove(filename, out _);
                return new FileAck { Ok = true, TotalSize = totalSizeFromReceiver};

            }
            catch (TaskCanceledException)
            {
                _logger.LogError("[SendFile] Upload stopped for={ChatId}", chatId);
                fileTransfer.UploadCompleteTcs.TrySetCanceled(context.CancellationToken.IsCancellationRequested ? context.CancellationToken : new CancellationToken(true));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[SendFile] Upload stopped for={ChatId}", chatId);
                fileTransfer.UploadCompleteTcs.TrySetException(e);
            } finally 
            {
                await enumerator.DisposeAsync();
                session.FileTransfers.TryRemove(filename, out _);
            }
            
            return new FileAck { Ok = false, TotalSize = fileTransfer.TotalSize };
        }
        

        public override async Task ReceiveFile(FileRequest request, IServerStreamWriter<FileChunk> responseStream, ServerCallContext context)
        {
            _logger.LogInformation("[ReceiveFile] Peer={Peer} requesting file {File}", context.Peer, request.FileName);
            
            string username = context.UserState["username"] as string ?? throw new RpcException(
                new Status(StatusCode.Unauthenticated, "No JWT info inside"));
        
            var room = await _rooms.Find(r => r.ChatId == request.ChatId).FirstOrDefaultAsync(context.CancellationToken);

            if (room == null || (room.SubscriberUsername != username && room.OwnerUsername != username))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Not belongs to room"));
            }
            
            var filename = $"{request.ChatId}.{request.FileName.ToStringUtf8()}";

            if (!_sessions.ChatSessions.TryGetValue(request.ChatId, out var session) || session.MessageSubscribers.Count != 2)
            {
                _logger.LogWarning("[ReceiveFile] No subscribers for ChatId={ChatId}", request.ChatId);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "No peer connection"));
            }
            
            if (!session.FileTransfers.TryGetValue(filename, out var fileTransfer))
            {
                _logger.LogWarning("[ReceiveFile] No subscribers for ChatId={ChatId}", request.ChatId);
                throw new RpcException(new Status(StatusCode.InvalidArgument, "No such file transfer"));
            }

            await using var reg = context.CancellationToken.Register(( ) =>
            { 
                _logger.LogWarning("[ReceiveFile] Peer disconnected");
                fileTransfer.LifetimeCts.Cancel();
                fileTransfer.UploadCompleteTcs.TrySetException(
                    new RpcException(new Status(StatusCode.Aborted, "aborted by other peer")));
            });

            try
            {
                fileTransfer.LifetimeCts.Token.ThrowIfCancellationRequested();
                fileTransfer.TotalSize += fileTransfer.FirstChunk.Data.Length;
                
                await responseStream.WriteAsync(fileTransfer.FirstChunk);
                while (await fileTransfer.SenderStream.MoveNextAsync())
                {
                    fileTransfer.LifetimeCts.Token.ThrowIfCancellationRequested();
                    var chunk = fileTransfer.SenderStream.Current;
                    await responseStream.WriteAsync(chunk, context.CancellationToken);
                    fileTransfer.TotalSize += chunk.Data.Length;
                }

                fileTransfer.LifetimeCts.Token.ThrowIfCancellationRequested();
                fileTransfer.UploadCompleteTcs.TrySetResult(fileTransfer.TotalSize);
            }

            catch (OperationCanceledException op) 
            {
                _logger.LogWarning(op, "[ReceiveFile] Operation was canceled during file streaming for ChatId={ChatId}, Filename={Filename}", request.ChatId, request.FileName);
                if (!fileTransfer.LifetimeCts.IsCancellationRequested)
                {
                    await fileTransfer.LifetimeCts.CancelAsync();
                }
                fileTransfer.UploadCompleteTcs.TrySetCanceled(op.CancellationToken.IsCancellationRequested ? op.CancellationToken : new CancellationToken(true));
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReceiveFile] Error during file streaming for ChatId={ChatId}, Filename={Filename}", request.ChatId, request.FileName);
                if (!fileTransfer.LifetimeCts.IsCancellationRequested)
                {
                    await fileTransfer.LifetimeCts.CancelAsync();
                }
                fileTransfer.UploadCompleteTcs.TrySetException(
                    ex is RpcException rpcEx ? rpcEx : new RpcException(new Status(StatusCode.Internal, $"ReceiveFile failed: {ex.Message}"))
                );
                throw;
            }
            _logger.LogInformation("[ReceiveFile] Completed streaming file {File}", request.FileName);
        }

        public override async Task GetAllJoinedRooms(Empty request, IServerStreamWriter<RoomInfo> responseStream, ServerCallContext context)
        {
            string from = context.UserState["username"] as string ?? throw new RpcException(
                new Status(StatusCode.Unauthenticated, "No JWT info inside"));
            
            _logger.LogTrace("[GetAllJoinedRooms] Peer={Peer} requesting all rooms {File}", context.Peer, from);

            var rooms = await _rooms.Find(r => r.OwnerUsername == from || r.SubscriberUsername == from).ToListAsync();

            foreach (var room in rooms)
            {
                await responseStream.WriteAsync(new RoomInfo()
                {
                    ChatId = room.ChatId,
                    CreationTime = room.CreationTime.ToString("o"),
                    OwnerUsername = room.OwnerUsername,
                    OtherSubscriber = room.SubscriberUsername ?? "Никто",
                    Settings = new RoomData()
                    {
                        Algo = room.Algo,
                        CipherMode = room.CipherMode,
                        Padding = room.PaddingMode
                    }
                });
            }
            
            _logger.LogTrace("[GetAllJoinedRooms] Completed streaming all rooms {File}", from);
        }
}