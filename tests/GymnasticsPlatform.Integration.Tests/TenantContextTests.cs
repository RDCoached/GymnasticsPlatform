using System.Security.Claims;
using Common.Core;
using FluentAssertions;
using GymnasticsPlatform.Api.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class TenantContextTests
{
    [Fact]
    public void TenantId_ReturnsTenantIdFromClaim_WhenClaimExists()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var httpContextAccessor = CreateHttpContextAccessorWithClaim("tenant_id", expectedTenantId.ToString());
        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var actualTenantId = tenantContext.TenantId;

        // Assert
        actualTenantId.Should().Be(expectedTenantId);
    }

    [Fact]
    public void TenantId_ReturnsNull_WhenClaimDoesNotExist()
    {
        // Arrange
        var httpContextAccessor = CreateHttpContextAccessorWithNoClaims();
        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        tenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_ReturnsNull_WhenClaimValueIsNotValidGuid()
    {
        // Arrange
        var httpContextAccessor = CreateHttpContextAccessorWithClaim("tenant_id", "not-a-guid");
        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        tenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_ReturnsNull_WhenClaimValueIsEmpty()
    {
        // Arrange
        var httpContextAccessor = CreateHttpContextAccessorWithClaim("tenant_id", string.Empty);
        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        tenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_ReturnsNull_WhenHttpContextIsNull()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        tenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_ReturnsNull_WhenUserIsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = null!;

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        tenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_IgnoresOtherClaims()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "user-123"),
                new Claim("email", "test@example.com"),
                new Claim("tenant_id", expectedTenantId.ToString()),
                new Claim("roles", "user"),
            ]))
        };

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        tenantId.Should().Be(expectedTenantId);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("123e4567-e89b-12d3-a456-426614174000")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    public void TenantId_ParsesVariousGuidFormats(string guidString)
    {
        // Arrange
        var expectedGuid = Guid.Parse(guidString);
        var httpContextAccessor = CreateHttpContextAccessorWithClaim("tenant_id", guidString);
        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var tenantId = tenantContext.TenantId;

        // Assert
        tenantId.Should().Be(expectedGuid);
    }

    [Fact]
    public void TenantContext_ImplementsITenantContext()
    {
        // Arrange
        var httpContextAccessor = CreateHttpContextAccessorWithNoClaims();

        // Act
        var tenantContext = new TenantContext(httpContextAccessor);

        // Assert
        tenantContext.Should().BeAssignableTo<ITenantContext>();
    }

    private static IHttpContextAccessor CreateHttpContextAccessorWithClaim(string claimType, string claimValue)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, claimValue)]))
        };

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        return httpContextAccessor;
    }

    private static IHttpContextAccessor CreateHttpContextAccessorWithNoClaims()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        return httpContextAccessor;
    }
}
