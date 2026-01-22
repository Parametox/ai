namespace KanbanLite.E2E.Tests.Infrastructure;

/// <summary>
/// Kolekcja testów dzielących tę samą instancję Playwright.
/// </summary>
[CollectionDefinition(nameof(PlaywrightCollection))]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
}
