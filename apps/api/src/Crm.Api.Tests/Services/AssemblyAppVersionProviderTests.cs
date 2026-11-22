using Crm.Api.Services;
using Shouldly;

namespace Crm.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AssemblyAppVersionProvider"/>.
/// </summary>
public class AssemblyAppVersionProviderTests
{
    [Fact]
    public void DefaultConstructorShouldReturnLocalDevVersion()
    {
        var provider = new AssemblyAppVersionProvider();

        provider.Version.ShouldBe("dev");
    }
}
