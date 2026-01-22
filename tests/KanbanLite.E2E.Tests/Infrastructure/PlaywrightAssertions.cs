using Microsoft.Playwright;

namespace KanbanLite.E2E.Tests.Infrastructure;

/// <summary>
/// Statyczny helper dla asercji Playwright w testach xUnit.
/// Dostarcza metody Expect() kompatybilne z Playwright API.
/// </summary>
public static class PlaywrightAssertions
{
    /// <summary>
    /// Tworzy asercje dla IPage.
    /// </summary>
    public static IPageAssertions Expect(IPage page) => Assertions.Expect(page);

    /// <summary>
    /// Tworzy asercje dla ILocator.
    /// </summary>
    public static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

    /// <summary>
    /// Tworzy asercje dla IAPIResponse.
    /// </summary>
    public static IAPIResponseAssertions Expect(IAPIResponse response) => Assertions.Expect(response);
}
