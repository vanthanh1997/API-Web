namespace Infrastructure.Exporting
{
    internal static class SeekableStreamAdapter
    {
        /// <summary>File nhỏ hơn ngưỡng này thì đệm trong RAM, lớn hơn thì tràn ra đĩa.</summary>
        private const int MemoryThreshold = 32 * 1024 * 1024;

        /// <summary>
        /// Đích đọc/seek được (VD FileStream mở ReadWrite) → ghi thẳng, không tốn thêm gì.
        /// Đích chỉ-ghi (Response.Body) → đệm qua stream trung gian rồi copy sang.
        /// </summary>
        public static async Task WriteViaSeekableAsync(
            Stream destination,
            Action<Stream> write,
            CancellationToken ct)
        {
            if (destination is { CanRead: true, CanSeek: true })
            {
                write(destination);
                return;
            }

            await using var buffer = new FileBackedStream(MemoryThreshold);

            write(buffer);

            buffer.Position = 0;
            await buffer.CopyToAsync(destination, 64 * 1024, ct);
        }
    }

    /// <summary>
    /// Giữ trong RAM khi còn nhỏ, tự tràn sang file tạm khi vượt ngưỡng — nhờ vậy
    /// xuất 1 triệu dòng qua Response.Body cũng không ôm cả file trong bộ nhớ.
    /// File tạm dùng FileOptions.DeleteOnClose nên tự xoá kể cả khi process chết.
    /// </summary>
    internal sealed class FileBackedStream(int memoryThreshold) : Stream
    {
        private Stream _inner = new MemoryStream();

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            SpillIfNeeded(count);
            _inner.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            SpillIfNeeded(1);
            _inner.WriteByte(value);
        }

        private void SpillIfNeeded(int incoming)
        {
            if (_inner is not MemoryStream memory) return;
            if (memory.Length + incoming <= memoryThreshold) return;

            var path = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.tmp");

            var file = new FileStream(
                path, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                64 * 1024, FileOptions.DeleteOnClose);

            memory.Position = 0;
            memory.CopyTo(file);
            memory.Dispose();

            _inner = file;
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Flush() => _inner.Flush();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();

            base.Dispose(disposing);
        }
    }
}
