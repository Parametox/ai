using DataAccess.Entities;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;
using FluentValidation;
using AutoMapper;

namespace KanbanLite.Application.UnitTests.Services;

public class OrderServiceTests : TestBase
{
    private readonly OrderService _sut;
    private readonly IValidator<CreateOrderRequest> _createOrderValidator;
    private readonly IMapper _mapper;

    public OrderServiceTests()
    {
        _createOrderValidator = Substitute.For<IValidator<CreateOrderRequest>>();
        _mapper = Substitute.For<IMapper>();
        _sut = new OrderService(DbFactory, CurrentUser, _createOrderValidator, _mapper);
    }

    [Fact]
    public async Task CreateAsync_WithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(false);

        // Act
        var result = await _sut.CreateAsync(new CreateOrderRequest("", 0, 0, DateOnly.MinValue));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ReturnsValidationFailed()
    {
         // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);

        var validationResult = new FluentValidation.Results.ValidationResult(
            new[] { new FluentValidation.Results.ValidationFailure("Quantity", "Quantity must be greater than 0") }
        );
        _createOrderValidator.ValidateAsync(Arg.Any<CreateOrderRequest>(), Arg.Any<CancellationToken>())
            .Returns(validationResult);

        // Act
        // Quantity 0 is invalid
        var result = await _sut.CreateAsync(new CreateOrderRequest("ORD-1", 0, 1, DateOnly.FromDateTime(DateTime.Today.AddDays(7))));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task CreateAsync_WithInactiveProductFormat_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);
        
        _createOrderValidator.ValidateAsync(Arg.Any<CreateOrderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult());
        
        var formatId = 10L;
        DbContext.ProductFormats.Add(new ProductFormat { Id = formatId, Name = "A5", IsActive = false });
        await DbContext.SaveChangesAsync();

        var request = new CreateOrderRequest("ORD-1", 100, formatId, DateOnly.FromDateTime(DateTime.Today.AddDays(10)));

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("nieaktywny");
    }

    [Fact]
    public async Task CreateAsync_WithoutMatchingSplitRule_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);
        
        _createOrderValidator.ValidateAsync(Arg.Any<CreateOrderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult());
        
        var formatId = 20L;
        DbContext.ProductFormats.Add(new ProductFormat { Id = formatId, Name = "A5", IsActive = true });
        // No split rules in DB
        await DbContext.SaveChangesAsync();

        var request = new CreateOrderRequest("ORD-1", 100, formatId, DateOnly.FromDateTime(DateTime.Today.AddDays(10)));

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("Brak aktywnej reguły dzielenia");
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesOrderProjectAndBatches()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);
        
        _createOrderValidator.ValidateAsync(Arg.Any<CreateOrderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult());
        
        var formatId = 30L;
        DbContext.ProductFormats.Add(new ProductFormat { Id = formatId, Name = "A5", IsActive = true });
        
        DbContext.BatchSplitRules.Add(new BatchSplitRule 
        { 
            Id = 1, 
            MinQty = 1, 
            MaxQty = 100, 
            Percent = 0.5m, 
            MinBatchSize = 10 
        });
        await DbContext.SaveChangesAsync();
        // MaxQty=100. Percent=0.5 -> maxBatchSize = 50.
        // OrderQty=80. Batch1=50, Batch2=30.

        var request = new CreateOrderRequest("ORD-2026-001", 80, formatId, DateOnly.FromDateTime(DateTime.Today.AddDays(10)));

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var order = DbContext.Orders.FirstOrDefault(x => x.OrderNumber == "ORD-2026-001");
        order.Should().NotBeNull();
        order!.Quantity.Should().Be(80);

        var project = DbContext.Projects.FirstOrDefault(x => x.OrderId == order.Id);
        project.Should().NotBeNull();
        project!.ProjectNumber.Should().Be("PRJ-2026-001"); 

        var batches = DbContext.Batches.Where(x => x.ProjectId == project.Id).ToList();
        batches.Should().HaveCount(2);
        batches.Sum(x => x.Quantity).Should().Be(80);
    }
}
