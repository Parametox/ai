using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Application.Services.SupabaseModels;
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
    private readonly IOrderRepository _orderRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly IProductFormatRepository _productFormatRepository;
    private readonly IBatchSplitRuleRepository _batchSplitRuleRepository;

    public OrderServiceTests()
    {
        _createOrderValidator = Substitute.For<IValidator<CreateOrderRequest>>();
        _mapper = Substitute.For<IMapper>();
        _orderRepository = Substitute.For<IOrderRepository>();
        _projectRepository = Substitute.For<IProjectRepository>();
        _batchRepository = Substitute.For<IBatchRepository>();
        _productFormatRepository = Substitute.For<IProductFormatRepository>();
        _batchSplitRuleRepository = Substitute.For<IBatchSplitRuleRepository>();

        _sut = new OrderService(
            _orderRepository,
            _projectRepository,
            _batchRepository,
            _productFormatRepository,
            _batchSplitRuleRepository,
            CurrentUser,
            _createOrderValidator,
            _mapper
        );
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
        _productFormatRepository.GetByIdAsync(formatId)
            .Returns(new SupabaseProductFormat { Id = formatId, Name = "A5", IsActive = false });

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
        _productFormatRepository.GetByIdAsync(formatId)
            .Returns(new SupabaseProductFormat { Id = formatId, Name = "A5", IsActive = true });
        
        _batchSplitRuleRepository.GetActiveRulesAsync()
            .Returns(new List<SupabaseBatchSplitRule>()); // Empty

        var request = new CreateOrderRequest("ORD-1", 100, formatId, DateOnly.FromDateTime(DateTime.Today.AddDays(10)));

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("Brak aktywnej reguły dzielenia batchy");
    }

    [Fact]
    public async Task CreateAsync_Success_CreatesEntities()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);
        _createOrderValidator.ValidateAsync(Arg.Any<CreateOrderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult());

        var formatId = 30L;
        _productFormatRepository.GetByIdAsync(formatId).Returns(new SupabaseProductFormat { Id = formatId, IsActive = true, Name = "PF" });
        
        // Rule: Min 100, Max null, Percent 1.0 (1 batch)
        _batchSplitRuleRepository.GetActiveRulesAsync().Returns(new List<SupabaseBatchSplitRule>
        {
            new SupabaseBatchSplitRule { MinQty = 1, MaxQty = null, Percent = 1.0m, MinBatchSize = 1, IsActive = true }
        });

        // Mock Creates returning instances
        _orderRepository.CreateAsync(Arg.Any<SupabaseOrder>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                var o = call.Arg<SupabaseOrder>();
                o.Id = 123;
                return o;
            });

        _projectRepository.CreateAsync(Arg.Any<SupabaseProject>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                var p = call.Arg<SupabaseProject>();
                p.Id = 456;
                return p;
            });

        var request = new CreateOrderRequest("ORD-SUCCESS", 500, formatId, DateOnly.MaxValue);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _orderRepository.Received(1).CreateAsync(Arg.Is<SupabaseOrder>(o => o.OrderNumber == "ORD-SUCCESS"), Arg.Any<CancellationToken>());
        await _projectRepository.Received(1).CreateAsync(Arg.Is<SupabaseProject>(p => p.OrderId == 123), Arg.Any<CancellationToken>());
        await _batchRepository.Received(1).CreateRangeAsync(Arg.Is<IEnumerable<SupabaseBatch>>(b => b.Count() == 1), Arg.Any<CancellationToken>());
    }
}
