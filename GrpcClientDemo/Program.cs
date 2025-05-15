
using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using StainsGate;


namespace GrpcClientDemo
{
    class Program
    {
        // Здесь будем хранить текущий токен
        private static string _jwtToken;

        static async Task Main(string[] args)
        {
            // Создаём канал с автоматическим добавлением заголовка Authorization
            using var channel = GrpcChannel.ForAddress("http://localhost:5214");
            // Интерсептор, который добавляет [authorization: Bearer _jwtToken]
            var callInvoker = channel.Intercept(new AuthInterceptor(() => _jwtToken));

            // Клиенты
            var authClient = new Authentication.AuthenticationClient(callInvoker);
            var hackingClient = new HackingGate.HackingGateClient(callInvoker);

            // 1. Регистрация
            var regResp = await authClient.RegisterAsync(new RegisterRequestData
            {
                Username = "alice" + Random.Shared.Next(),
                PasswordHash = "p@ssw0rd"
            });
            Console.WriteLine($"Registered. Token: {regResp.Token}");
            _jwtToken = regResp.Token;

            // 2. Валидация токена
            var valResp = await authClient.ValidateTokenAsync(new ValidateRequest { Token = _jwtToken });
            Console.WriteLine($"Token valid: {valResp.Valid}, user: {valResp.Username}");

            // 3. Создание комнаты для чата
            var room = await hackingClient.CreateRoomAsync(new RoomData { EncryptAlgo = EncryptAlgo.Twofish });
            Console.WriteLine($"Room created: {room.ChatId}");

            // 4. Обмен DH‑параметрами (упрощённый пример)
            using var dhCall = hackingClient.ExchangeDhParameters();
            // Клиент шлёт свою публичную часть
            await dhCall.RequestStream.WriteAsync(new ExchangeData
            {
                ChatId = room.ChatId,
                PublicPart = Google.Protobuf.ByteString.CopyFromUtf8("clientPublic")
            });
            // Читает часть от сервера
            if (await dhCall.ResponseStream.MoveNext())
            {
                var serverPart = dhCall.ResponseStream.Current.PublicPart.ToStringUtf8();
                Console.WriteLine($"Received server DH part: {serverPart}");
            }
            await dhCall.RequestStream.CompleteAsync();

            // 5. Отправка и приём сообщения
            // Подписываемся на входящие
            var recvCall = hackingClient.ReceiveMessages(new RoomPassKey { ChatId = room.ChatId });
            var readTask = Task.Run(async () =>
            {
                await foreach (var msg in recvCall.ResponseStream.ReadAllAsync())
                {
                    Console.WriteLine($"Received message ({msg.ChatId}): {msg.Data.ToStringUtf8()}");
                }
            });

            // Шлём тестовое сообщение
            var ack = await hackingClient.SendMessageAsync(new Message
            {
                ChatId = room.ChatId,
                Data = Google.Protobuf.ByteString.CopyFromUtf8("Hello from client")
            });
            Console.WriteLine($"Message ack: ok={ack.Ok}");

            // Ждём чуть‑чуть, чтобы получить ответ
            await Task.Delay(2000);

            // Завершаем
            //recvCall.Dispose(); // при необходимости
            Console.WriteLine("Client done.");
        }
    }

    // Интерсептор для автоматической вставки токена в metadata
    public class AuthInterceptor : Interceptor
    {
        private readonly Func<string> _getToken;
        public AuthInterceptor(Func<string> getToken) => _getToken = getToken;

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var headers = context.Options.Headers ?? new Metadata();
            var token = _getToken();
            if (!string.IsNullOrEmpty(token))
                headers.Add("authorization", $"Bearer {token}");
            var options = context.Options.WithHeaders(headers);
            return continuation(request, new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options));
        }

        // Аналогично можно переопределить Streaming методы...
    }
}
