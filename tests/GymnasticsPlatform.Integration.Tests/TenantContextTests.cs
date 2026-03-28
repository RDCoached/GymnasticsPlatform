using Common.Core;
using FluentAssertions;
using GymnasticsPlatform.Api.Middleware;
using GymnasticsPlatform.Api.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class TenantContextTests
{
    [Fact]
    public void TenantId_ReturnsTenantIdFromHttpContextItems_WhenItemExists()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var httpContextAccessor = CreateHttpContextAccessorWithTenantId(expectedTenantId);
        var tenantContext = new TenantContext(httpContextAccessor);

        // Act
        var actualTenantId = tenantContext.TenantId;

        // Assert
        actualTenantId.Should().Be(expectedTenantId);
    }

    [Fact]
    public void TenantId_ReturnsNull_WhenItemDoesNotExist()
    {
        // Arrange
        var httpContextAccessor = CreateHttpContextAccessorWithNoTenantId();
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
    public void TenantContext_ImplementsITenantContext()
    {
        // Arrange
        var httpContextAccessor = CreateHttpContextAccessorWithNoTenantId();

        // Act
        var tenantContext = new TenantContext(httpContextAccessor);

        // Assert
        tenantContext.Should().BeAssignableTo<ITenantContext>();
    }

    private static IHttpContextAccessor CreateHttpContextAccessorWithTenantId(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[TenantResolutionMiddleware.TenantIdKey] = tenantId;

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        return httpContextAccessor;
    }

    private static IHttpContextAccessor CreateHttpContextAccessorWithNoTenantId()
    {
        var httpContext = new DefaultHttpContext();

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        return httpContextAccessor;
    }
}
