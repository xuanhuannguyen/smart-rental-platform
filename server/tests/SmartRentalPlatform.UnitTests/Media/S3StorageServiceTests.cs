using System.Reflection;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Infrastructure.Options;
using SmartRentalPlatform.Infrastructure.Storage;

namespace SmartRentalPlatform.UnitTests.Media;

public sealed class S3StorageServiceTests
{
    [Fact]
    public async Task UploadAsync_NonSeekableStream_ShouldPassDeclaredContentLengthToS3()
    {
        var contentBytes = new byte[] { 1, 2, 3, 4 };
        await using var content = new NonSeekableReadStream(new MemoryStream(contentBytes));
        var s3Client = DispatchProxy.Create<IAmazonS3, RecordingS3Proxy>();
        var recorder = (RecordingS3Proxy)(object)s3Client;
        var service = new S3StorageService(
            s3Client,
            Options.Create(new S3StorageOptions { BucketName = "test-media-bucket" }));

        await service.UploadAsync(new MediaUploadRequest
        {
            Content = content,
            OriginalFileName = "rooming-house.png",
            ContentType = "image/png",
            FileSize = contentBytes.Length,
            ObjectKey = "public/rooming-house-images/rooming-house.png",
            Visibility = MediaVisibility.Public
        });

        Assert.NotNull(recorder.LastPutRequest);
        Assert.Equal(contentBytes.Length, recorder.LastPutRequest.Headers.ContentLength);
        Assert.Same(content, recorder.LastPutRequest.InputStream);
    }

    private class RecordingS3Proxy : DispatchProxy
    {
        public PutObjectRequest? LastPutRequest { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IAmazonS3.PutObjectAsync))
            {
                LastPutRequest = Assert.IsType<PutObjectRequest>(args?[0]);
                return Task.FromResult(new PutObjectResponse());
            }

            if (targetMethod?.Name == nameof(IDisposable.Dispose))
            {
                return null;
            }

            throw new NotSupportedException($"Unexpected S3 call: {targetMethod?.Name}");
        }
    }

    private sealed class NonSeekableReadStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
