namespace PixConvert.Tests;

public class AppTests
{
    [Fact]
    public void DisposeServices_WhenProviderIsDisposable_ShouldDisposeProviderOnce()
    {
        var provider = new DisposableServiceProvider();

        PixConvert.App.DisposeServices(provider);

        Assert.Equal(1, provider.DisposeCount);
    }

    [Fact]
    public void DisposeServices_WhenProviderDisposeThrows_ShouldNotThrow()
    {
        var provider = new ThrowingDisposableServiceProvider();

        var exception = Record.Exception(() => PixConvert.App.DisposeServices(provider));

        Assert.Null(exception);
        Assert.Equal(1, provider.DisposeCount);
    }

    private sealed class DisposableServiceProvider : IServiceProvider, IDisposable
    {
        public int DisposeCount { get; private set; }

        public object? GetService(Type serviceType) => null;

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class ThrowingDisposableServiceProvider : IServiceProvider, IDisposable
    {
        public int DisposeCount { get; private set; }

        public object? GetService(Type serviceType) => null;

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("dispose failed");
        }
    }
}
