using Gateway.Areas.Admin.Services;
using Xunit;

namespace Data.Tests;

public class TemporaryPasswordGeneratorTests
{
    [Fact]
    public void Generate_DefaultLength_Returns12Characters()
    {
        var password = TemporaryPasswordGenerator.Generate();
        Assert.Equal(12, password.Length);
    }

    [Fact]
    public void Generate_CustomLength_ReturnsRequestedLength()
    {
        var password = TemporaryPasswordGenerator.Generate(20);
        Assert.Equal(20, password.Length);
    }

    [Fact]
    public void Generate_CalledTwice_ProducesDifferentValues()
    {
        var first = TemporaryPasswordGenerator.Generate();
        var second = TemporaryPasswordGenerator.Generate();
        Assert.NotEqual(first, second);
    }
}