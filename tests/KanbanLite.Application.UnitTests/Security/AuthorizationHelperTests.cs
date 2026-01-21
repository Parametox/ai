using FluentAssertions;
using KanbanLite.Application.Security;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Security;

public class AuthorizationHelperTests
{
    private readonly ICurrentUser _currentUserSubstitute;

    public AuthorizationHelperTests()
    {
        _currentUserSubstitute = Substitute.For<ICurrentUser>();
    }

    [Fact]
    public void EnsureManagerAuthorized_ShouldReturnNull_WhenUserIsManager()
    {
        // Arrange
        _currentUserSubstitute.UserId.Returns("user-id");
        _currentUserSubstitute.IsInRole("Manager").Returns(true);

        // Act
        var result = AuthorizationHelper.EnsureManagerAuthorized(_currentUserSubstitute);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void EnsureManagerAuthorized_ShouldReturnUnauthorized_WhenUserIdIsNull()
    {
        // Arrange
        _currentUserSubstitute.UserId.Returns((string?)null);

        // Act
        var result = AuthorizationHelper.EnsureManagerAuthorized(_currentUserSubstitute);

        // Assert
        result.Should().NotBeNull();
        result!.Code.Should().Be("Unauthorized");
        result.Message.Should().Be("Użytkownik nie jest zalogowany.");
    }

    [Fact]
    public void EnsureManagerAuthorized_ShouldReturnForbidden_WhenUserIsNotManager()
    {
        // Arrange
        _currentUserSubstitute.UserId.Returns("user-id");
        _currentUserSubstitute.IsInRole("Manager").Returns(false);

        // Act
        var result = AuthorizationHelper.EnsureManagerAuthorized(_currentUserSubstitute);

        // Assert
        result.Should().NotBeNull();
        result!.Code.Should().Be("Forbidden");
        result.Message.Should().Contain("Brak uprawnień");
    }

    [Fact]
    public void EnsureManagerOrOperatorAuthorized_ShouldReturnNull_WhenUserIsManager()
    {
        // Arrange
        _currentUserSubstitute.UserId.Returns("user-id");
        _currentUserSubstitute.IsInRole("Manager").Returns(true);

        // Act
        var result = AuthorizationHelper.EnsureManagerOrOperatorAuthorized(_currentUserSubstitute);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void EnsureManagerOrOperatorAuthorized_ShouldReturnNull_WhenUserIsOperator()
    {
        // Arrange
        _currentUserSubstitute.UserId.Returns("user-id");
        _currentUserSubstitute.IsInRole("Operator").Returns(true);

        // Act
        var result = AuthorizationHelper.EnsureManagerOrOperatorAuthorized(_currentUserSubstitute);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void EnsureManagerOrOperatorAuthorized_ShouldReturnForbidden_WhenUserIsNeither()
    {
        // Arrange
        _currentUserSubstitute.UserId.Returns("user-id");
        _currentUserSubstitute.IsInRole("Manager").Returns(false);
        _currentUserSubstitute.IsInRole("Operator").Returns(false);

        // Act
        var result = AuthorizationHelper.EnsureManagerOrOperatorAuthorized(_currentUserSubstitute);

        // Assert
        result.Should().NotBeNull();
        result!.Code.Should().Be("Forbidden");
    }
}
