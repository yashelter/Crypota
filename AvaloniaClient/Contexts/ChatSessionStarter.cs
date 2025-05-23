using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaClient.Services;
using Google.Protobuf;
using Grpc.Core;
using Serilog;
using StainsGate;
using static Crypota.DiffieHellman.Protocol;

namespace AvaloniaClient.Contexts;


public class ChatSessionStarter
{
    public string ChatId { get; }

    public byte[]? OwnDhPrivateKey { get; private set; }
    public byte[]? OwnDhPublicKey { get; private set; }
    
    private byte[]? _gValue;
    private byte[]? _pValue;
    public byte[]? SharedSecret { get; private set; } // this we get
    public bool IsDhComplete { get; private set; } = false;
    
    
    public event Action<string, string>? OnDhError; 
    public event Action<string>? OnDhCompleted;
    

    public ChatSessionStarter(string chatId)
    {
        ChatId = chatId;
        Log.Debug("ChatSessionContext создан для ChatId: {0}", ChatId);
    }

    private async Task InitPublicConstants(CancellationToken ct)
    {
        Log.Debug("Запрос параметров Диффи-Хеллмана (g, p) для чата {0}", ChatId);
        using var instance = ServerApiClient.Instance;

        DiffieHellmanData dhConstants = await instance.GrpcClient.GetPublicDhParametersAsync(
            new DiffieHellmanQuery { ChatId = this.ChatId }, cancellationToken: ct);
        
        _gValue = dhConstants.GValue.ToByteArray();
        _pValue = dhConstants.PValue.ToByteArray();
        
        Log.Information("Получены параметры Диффи-Хеллмана (g, p) для чата {0}", ChatId);
    }
    
    
    public async Task InitializeSessionAsync(CancellationToken externalCt = default)
    {
        Log.Information("Инициализация сессии для чата {0}", ChatId);
        
        ResetDhState();

        try
        {
            if (_gValue == null || _pValue == null)
            {
                await InitPublicConstants(externalCt);
                if (_gValue == null || _pValue == null)
                {
                    throw new NotSupportedException("Something ultra weird");
                }
            }

            BigInteger secret = await Task.Run(() => GenerateSecret(), externalCt);

            BigInteger publicKey =  await Task.Run(() => GenerateDhKeys(GetBigIntegerFromArray(_gValue), secret, 
                GetBigIntegerFromArray(_pValue)), externalCt);
            
            OwnDhPrivateKey = secret.ToByteArray();
            OwnDhPublicKey = publicKey.ToByteArray();
                
            Log.Debug("Сгенерирована собственная пара ключей DH для чата {ChatId}", ChatId);

            await PerformDhKeyExchangeAsync(externalCt);

            if (IsDhComplete)
            {
                Log.Information("Обмен был успешен для чата {0}", ChatId);
                OnDhCompleted?.Invoke(ChatId);
            }
            else
            {
                 Log.Error("DH обмен не был завершен для чата {0}. Подписка на сообщения не запущена.", ChatId);
                 OnDhError?.Invoke(ChatId, "Обмен ключами Диффи-Хеллмана не был завершен.");
            }
        }
        catch (RpcException ex)
        {
            Log.Error(ex, "gRPC ошибка во время инициализации сессии для чата {0}", ChatId);
            OnDhError?.Invoke(ChatId, $"Ошибка gRPC: {ex.Status.Detail}");
            ResetDhState();
        }
        catch (OperationCanceledException ex)
        {
            Log.Error(ex, "Инициализация сессии для чата {0} была отменена.", ChatId);
            OnDhError?.Invoke(ChatId, "Операция отменена.");
            ResetDhState();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Общая ошибка во время инициализации сессии для чата {0}", ChatId);
            OnDhError?.Invoke(ChatId, $"Внутренняя ошибка: {ex.Message}");
            ResetDhState();
        }
    }

    private async Task PerformDhKeyExchangeAsync(CancellationToken ct)
    {
        if (OwnDhPublicKey == null || _gValue == null || _pValue == null || OwnDhPrivateKey == null)
        {
            throw new InvalidOperationException("Невозможно начать обмен DH: собственные ключи или параметры g/p не установлены.");
        }

        var exchangeRequest = new ExchangeData
        {
            ChatId = this.ChatId,
            PublicPart = ByteString.CopyFrom(OwnDhPublicKey)
        };

        Log.Information("Начинаем streaaaaming для обмена DH ключами для чата {0}", ChatId);
        
        try
        {
            using var instance = ServerApiClient.Instance;
            using var call = instance.GrpcClient.ExchangeDhParameters(exchangeRequest, cancellationToken: ct);

            await foreach (var mateData in call.ResponseStream.ReadAllAsync(ct))
            {
                Log.Debug("Получена публичная часть DH от собеседника для чата {0}", ChatId);
                byte[] matePublicKey = mateData.PublicPart.ToByteArray();

                BigInteger a = GetBigIntegerFromArray(OwnDhPrivateKey);
                BigInteger b = GetBigIntegerFromArray(matePublicKey);
                BigInteger p = GetBigIntegerFromArray(_pValue);

                BigInteger calculatedSharedSecret = CalculateSharedSecret(a, b, p);

                SharedSecret = calculatedSharedSecret.ToByteArray();
                IsDhComplete = true;

                Log.Information("Обмен DH ключами успешно завершен для чата {0}. Общий секрет вычислен.", ChatId);

                break /*as we have only two friends per chat*/;
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Log.Information(ex, "Стрим обмена DH для чата {0} был отменен.", ChatId);
            ResetDhState();
            throw;
        }
        catch (RpcException ex)
        {
            Log.Error(ex, "gRPC ошибка во время обмена DH ключами для чата {0}", ChatId);
            ResetDhState();
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Something unexpected happened");
            ResetDhState(); 
            throw;
        }
    }

    public void ResetDhState()
    {
        OwnDhPrivateKey = null;
        OwnDhPublicKey = null;
        _gValue = null;
        _pValue = null;
        SharedSecret = null;
        IsDhComplete = false;
    }
    
}