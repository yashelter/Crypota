using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Models
{
    public class EncryptedFile: IDisposable
    {
        public int ChunkSizeBytes { get; init; } = 3 * 1024 * 1024;
        
        private readonly string _path;
        private long _cursor = 0;
        
        private bool _disposed = false;

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public EncryptedFile(string path)
        {
            _path = path;
        }

        public async Task AppendFragmentAsync(byte[] bytes)
        {
            await _semaphore.WaitAsync();
            try
            {
                // Дозапись в файл
                await File.AppendAllBytesAsync(_path, bytes);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        /// <summary>
        /// Записывает байты в файл, начиная с указанного смещения.
        /// </summary>
        public async Task AppendFragmentAtOffsetAsync(long offset, byte[] bytes)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var fs = new FileStream(
                    _path,
                    FileMode.OpenOrCreate,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true
                );
                fs.Seek(offset, SeekOrigin.Begin);
                await fs.WriteAsync(bytes, 0, bytes.Length);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Читает следующий фрагмент файла, возвращая сам фрагмент и смещение, с которого он прочитан.
        /// Возвращает null, когда до конца файла.</summary>
        public async Task<(byte[] Data, long Offset)?> ReadNextFragmentAsync(int chunkSizeBytes = 3 * 1024 * 1024)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!File.Exists(_path))
                    return null;

                var fileInfo = new FileInfo(_path);
                if (_cursor >= fileInfo.Length)
                    return null;

                using var fs = new FileStream(
                    _path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: chunkSizeBytes,
                    useAsync: true
                );
                fs.Seek(_cursor, SeekOrigin.Begin);

                int bytesToRead = (int)Math.Min(chunkSizeBytes, fileInfo.Length - _cursor);
                var buffer = new byte[bytesToRead];
                int read = await fs.ReadAsync(buffer, 0, bytesToRead);

                var offset = _cursor;
                _cursor += read;
                return (buffer, offset);
            }
            finally
            {
                _semaphore.Release();
            }
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _semaphore?.Dispose();
            }
            _disposed = true;
        }

        ~EncryptedFile()
        {
            Dispose(false);
        }
    }
    
}