using DataAccess.Entities;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class ProductFormatServiceTests : TestBase
{
    private readonly ProductFormatService _sut;

    public ProductFormatServiceTests()
    {
        _sut = new ProductFormatService(DbFactory, CurrentUser);
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithNullQuery_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.GetAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task GetAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

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
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(false);

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
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format1 = new ProductFormat
        {
            Id = 1,
            Name = "A4",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var format2 = new ProductFormat
        {
            Id = 2,
            Name = "A5",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DbContext.ProductFormats.AddRange(format1, format2);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 1, PageSize: 10));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_FilterByIsActive_ReturnsOnlyActiveFormats()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var activeFormat = new ProductFormat
        {
            Id = 1,
            Name = "A4",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var inactiveFormat = new ProductFormat
        {
            Id = 2,
            Name = "A5",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DbContext.ProductFormats.AddRange(activeFormat, inactiveFormat);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 1, PageSize: 10, IsActive: true));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Name.Should().Be("A4");
    }

    [Fact]
    public async Task GetAsync_WithSearchQuery_FiltersResults()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format1 = new ProductFormat { Id = 1, Name = "A4 Premium", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var format2 = new ProductFormat { Id = 2, Name = "B5 Standard", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var format3 = new ProductFormat { Id = 3, Name = "A4 Standard", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.ProductFormats.AddRange(format1, format2, format3);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 1, PageSize: 10, Q: "A4"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().AllSatisfy(f => f.Name.Should().Contain("A4"));
    }

    [Fact]
    public async Task GetAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        for (int i = 1; i <= 5; i++)
        {
            DbContext.ProductFormats.Add(new ProductFormat
            {
                Id = i,
                Name = $"Format{i}",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 2, PageSize: 2));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Page.Should().Be(2);
        result.Value.Total.Should().Be(5);
    }

    [Fact]
    public async Task GetAsync_WithInvalidPageSize_NormalizesToDefault()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Act - PageSize 0 should become 50
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 1, PageSize: 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task GetAsync_WithPageSizeOver200_ClampsTo200()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Act
        var result = await _sut.GetAsync(new ProductFormatQuery(Page: 1, PageSize: 500));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(200);
    }

    #endregion

    #region GetActiveLookupAsync Tests

    [Fact]
    public async Task GetActiveLookupAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.GetActiveLookupAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetActiveLookupAsync_WithActiveFormats_ReturnsOnlyActive()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var activeFormat1 = new ProductFormat { Id = 1, Name = "A4", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var activeFormat2 = new ProductFormat { Id = 2, Name = "A5", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var inactiveFormat = new ProductFormat { Id = 3, Name = "B4", IsActive = false, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.ProductFormats.AddRange(activeFormat1, activeFormat2, inactiveFormat);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetActiveLookupAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().NotContain(f => f.Name == "B4");
    }

    [Fact]
    public async Task GetActiveLookupAsync_ReturnsOrderedByName()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format1 = new ProductFormat { Id = 1, Name = "Z-Format", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var format2 = new ProductFormat { Id = 2, Name = "A-Format", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var format3 = new ProductFormat { Id = 3, Name = "M-Format", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.ProductFormats.AddRange(format1, format2, format3);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetActiveLookupAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.First().Name.Should().Be("A-Format");
        result.Value.Last().Name.Should().Be("Z-Format");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithNullRequest_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.CreateAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task CreateAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);
        var request = new CreateProductFormatRequest("A4", true);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new CreateProductFormatRequest("", true);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.FieldErrors.Should().ContainKey("name");
    }

    [Fact]
    public async Task CreateAsync_WithNameTooLong_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new CreateProductFormatRequest(
            new string('A', 201), // 201 characters
            true
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.FieldErrors.Should().ContainKey("name");
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesFormat()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new CreateProductFormatRequest(
            "A4 Premium",
            true
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("A4 Premium");
        result.Value.IsActive.Should().BeTrue();

        // Verify it was saved
        var saved = await DbContext.ProductFormats.FindAsync(result.Value.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithWhitespaceInName_TrimsWhitespace()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new CreateProductFormatRequest(
            "  A4 Premium  ",
            true
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("A4 Premium");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithNullRequest_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.UpdateAsync(1, null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task UpdateAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);
        var request = new UpdateProductFormatRequest("A4", true);

        // Act
        var result = await _sut.UpdateAsync(1, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentFormat_ReturnsNotFound()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new UpdateProductFormatRequest("A4", true);

        // Act
        var result = await _sut.UpdateAsync(999, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyName_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var existingFormat = new ProductFormat
        {
            Id = 1,
            Name = "A4",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.ProductFormats.Add(existingFormat);
        await DbContext.SaveChangesAsync();

        var request = new UpdateProductFormatRequest("", true);

        // Act
        var result = await _sut.UpdateAsync(1, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesFormat()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var existingFormat = new ProductFormat
        {
            Id = 1,
            Name = "A4",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.ProductFormats.Add(existingFormat);
        await DbContext.SaveChangesAsync();

        var request = new UpdateProductFormatRequest(
            "A4 Updated",
            false
        );

        // Act
        var result = await _sut.UpdateAsync(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("A4 Updated");
        result.Value.IsActive.Should().BeFalse();
    }

    #endregion

    #region DeactivateAsync Tests

    [Fact]
    public async Task DeactivateAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.DeactivateAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task DeactivateAsync_NonExistentFormat_ReturnsNotFound()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Act
        var result = await _sut.DeactivateAsync(999);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task DeactivateAsync_ActiveFormat_DeactivatesAndReturnsTrue()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format = new ProductFormat
        {
            Id = 1,
            Name = "A4",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.ProductFormats.Add(format);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeactivateAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_AlreadyInactiveFormat_ReturnsFalse()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format = new ProductFormat
        {
            Id = 1,
            Name = "A4",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.ProductFormats.Add(format);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeactivateAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(); // Already inactive
    }

    #endregion
}
