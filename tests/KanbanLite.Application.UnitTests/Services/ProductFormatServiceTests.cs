using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KanbanLite.Application.Common;
using KanbanLite.Application.Services;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class ProductFormatServiceTests
{
    private readonly IProductFormatRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ProductFormatService _sut;

    public ProductFormatServiceTests()
    {
        _repository = Substitute.For<IProductFormatRepository>();
        _currentUser = Substitute.For<ICurrentUser>();
        _sut = new ProductFormatService(_repository, _currentUser);
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithNullQuery_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.GetAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task GetAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        _currentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 1, PageSize: 10));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetAsync_UserWithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        _currentUser.UserId.Returns("user123");
        _currentUser.IsInRole("Manager").Returns(false);

        // Act
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 1, PageSize: 10));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task GetAsync_WithValidQuery_ReturnsPagedResults()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        _repository.GetAsync(Arg.Any<ProductFormatQuery>(), 1, 10)
            .Returns((
                new List<SupabaseProductFormat> { new() { Id = 1, Name = "A4" }, new() { Id = 2, Name = "A5" } },
                2
            ));

        // Act
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 1, PageSize: 10));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(2);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_NullDto_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.CreateAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesAndReturns()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        SupabaseProductFormat? created = null;
        await _repository.CreateAsync(Arg.Do<SupabaseProductFormat>(x => created = x));

        // Act
        var result = await _sut.CreateAsync(new CreateProductFormatRequest("A4", true));

        // Assert
        result.IsSuccess.Should().BeTrue();
        created.Should().NotBeNull();
        created!.Name.Should().Be("A4");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);
        _repository.GetByIdAsync(1).Returns((SupabaseProductFormat?)null);

        // Act
        var result = await _sut.UpdateAsync(1, new UpdateProductFormatRequest("A4", true));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task UpdateAsync_Valid_UpdatesAndReturns()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        var existing = new SupabaseProductFormat { Id = 1, Name = "Old", IsActive = false };
        _repository.GetByIdAsync(1).Returns(existing);

        // Act
        var result = await _sut.UpdateAsync(1, new UpdateProductFormatRequest("New", true));

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Is<SupabaseProductFormat>(x => x.Id == 1 && x.Name == "New" && x.IsActive == true));
    }

    #endregion

    #region DeactivateAsync Tests (Replaces DeleteAsync)

    [Fact]
    public async Task DeactivateAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);
        _repository.GetByIdAsync(1).Returns((SupabaseProductFormat?)null);

        // Act
        var result = await _sut.DeactivateAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task DeactivateAsync_Success_ReturnsTrue()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);
        var item = new SupabaseProductFormat { Id = 1, IsActive = true };
        _repository.GetByIdAsync(1).Returns(item);

        // Act
        var result = await _sut.DeactivateAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Is<SupabaseProductFormat>(x => x.Id == 1 && x.IsActive == false));
    }

    #endregion
}
