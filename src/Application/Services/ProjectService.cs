using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using static KanbanLite.Application.Security.AuthorizationHelper;

namespace KanbanLite.Application.Services;

public sealed class ProjectService(IProjectRepository projectRepository, IBatchRepository batchRepository, ICurrentUser currentUser) : IProjectService
{
    public async Task<Result<PagedResult<ProjectListItemDto>>> GetAsync(ProjectQuery query, CancellationToken ct = default)
    {
        if (query is null)
        {
            return Result<PagedResult<ProjectListItemDto>>.Fail(
                AppError.ValidationFailed("Brak parametrów zapytania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["query"] = ["Query jest wymagany."]
                }));
        }

        var authError = EnsureManagerOrOperatorAuthorized(currentUser, "Brak uprawnień do podglądu projektu.");
        if (authError is not null)
        {
            return Result<PagedResult<ProjectListItemDto>>.Fail(authError);
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => query.PageSize
        };

        try
        {
            var (items, total) = await projectRepository.GetProjectsAsync(query, page, pageSize, ct);

            var resultItems = items.Select(p => new ProjectListItemDto(
                p.Id,
                p.ProjectNumber ?? "",
                p.Order?.OrderNumber ?? "",
                p.Order != null ? DateOnly.FromDateTime(p.Order.DueDate) : DateOnly.MinValue,
                p.IsCompleted
            )).ToList();

            return Result<PagedResult<ProjectListItemDto>>.Ok(new PagedResult<ProjectListItemDto>(
                resultItems,
                page,
                pageSize,
                total
            ));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return Result<PagedResult<ProjectListItemDto>>.Fail(
                AppError.Unexpected($"Nieoczekiwany błąd podczas pobierania listy projektów: {ex.Message}"));
        }
    }

    public async Task<Result<ProjectDetailsDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var authError = EnsureManagerOrOperatorAuthorized(currentUser, "Brak uprawnień do podglądu projektu.");
        if (authError is not null)
        {
            return Result<ProjectDetailsDto>.Fail(authError);
        }

        try
        {
            var project = await projectRepository.GetByIdAsync(id, ct);
            if (project is null)
            {
                return Result<ProjectDetailsDto>.Fail(AppError.NotFound($"Projekt o id={id} nie istnieje."));
            }

            var batchDtos = project.Batches.OrderBy(b => b.BatchNo).Select(b => new ProjectBatchSummaryDto(
                b.Id,
                b.BatchNo,
                b.Quantity,
                Enum.TryParse<BatchStatus>(b.Status, out var status) ? status : BatchStatus.New,
                (ProductionStage)b.Stage,
                ProgressPercentFromStage((ProductionStage)b.Stage),
                b.UpdatedAt
            )).ToList();

            var allBatchesDone = project.Batches.Count > 0 && project.Batches.All(
                b => b.Status == BatchStatus.Done.ToString() && b.Stage == (short)ProductionStage.Ship);
            
            string? notReadyReason = null;
            if (project.Batches.Count == 0) notReadyReason = "Brak batchy.";
            else if (!allBatchesDone) notReadyReason = "Nie wszystkie batche są gotowe.";
            else if (project.IsCompleted) notReadyReason = "Już wysłany.";

            bool canShip = !project.IsCompleted && allBatchesDone;

            var completionDto = new ProjectCompletionDto(canShip, notReadyReason);

            var order = project.Order;
            var orderSummary = new ProjectOrderSummaryDto(
                project.OrderId,
                order?.OrderNumber ?? "",
                order?.Quantity ?? 0,
                order != null ? DateOnly.FromDateTime(order.DueDate) : DateOnly.MinValue,
                "" // ProductFormatName requires extra join
            );

            var dto = new ProjectDetailsDto(
                project.Id,
                project.ProjectNumber ?? "",
                orderSummary,
                project.IsCompleted,
                completionDto,
                batchDtos
            );

            return Result<ProjectDetailsDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return Result<ProjectDetailsDto>.Fail(AppError.Unexpected($"Błąd: {ex.Message}"));
        }
    }

    public async Task<Result<ShipProjectResult>> ShipToCustomerAsync(long projectId, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do wysyłki projektu.");
        if (authError is not null)
        {
            return Result<ShipProjectResult>.Fail(authError);
        }

        try
        {
            var project = await projectRepository.GetByIdAsync(projectId, ct);
            if (project is null)
            {
                return Result<ShipProjectResult>.Fail(AppError.NotFound($"Projekt o id={projectId} nie istnieje."));
            }

            if (project.IsCompleted)
            {
                return Result<ShipProjectResult>.Fail(AppError.Conflict("Projekt jest już oznaczony jako wysłany do klienta."));
            }

            if (project.Batches == null || !project.Batches.Any())
            {
                return Result<ShipProjectResult>.Fail(
                    AppError.ValidationFailed("Nie można wysłać projektu bez batchy."));
            }

            var hasNotReadyBatches = project.Batches.Any(
                x => x.Status != BatchStatus.Done.ToString() || x.Stage != (short)ProductionStage.Ship);

            if (hasNotReadyBatches)
            {
                return Result<ShipProjectResult>.Fail(
                    AppError.ValidationFailed(
                        "Nie można wysłać projektu do klienta — nie wszystkie batche są zakończone na etapie wysyłki.",
                        new Dictionary<string, IReadOnlyList<string>>
                        {
                            ["projectId"] =
                            [
                                "Warunek: wszystkie batche muszą mieć Status=Done oraz Stage=Ship."
                            ]
                        }));
            }

            project.IsCompleted = true;
            project.CompletedAt = DateTimeOffset.UtcNow;
            project.CompletedByUserId = currentUser.UserId!;

            await projectRepository.UpdateAsync(project, ct);

            return Result<ShipProjectResult>.Ok(new ShipProjectResult(
                Id: project.Id,
                IsCompleted: project.IsCompleted,
                CompletedAt: project.CompletedAt!.Value,
                CompletedByUserId: project.CompletedByUserId!
            ));
        }
        catch (Exception ex)
        {
             Console.WriteLine(ex);
             return Result<ShipProjectResult>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas wysyłki projektu do klienta."));
        }
    }

    public async Task<Result> DeleteAsync(long projectId, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do usuwania projektu.");
        if (authError is not null)
        {
            return Result.Fail(authError);
        }

        try
        {
            var project = await projectRepository.GetByIdAsync(projectId, ct);
            if (project is null)
            {
                return Result.Fail(AppError.NotFound($"Projekt o id={projectId} nie istnieje."));
            }

            // Must delete batches first due to FK constraints
            await batchRepository.DeleteByProjectIdAsync(projectId, ct);

            await projectRepository.DeleteAsync(projectId, ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return Result.Fail(
                AppError.Unexpected($"Nieoczekiwany błąd podczas usuwania projektu: {ex.Message}"));
        }
    }

    private static int ProgressPercentFromStage(ProductionStage stage)
    {
        return stage switch
        {
            ProductionStage.Design => 20,
            ProductionStage.Print => 40,
            ProductionStage.Cut => 60,
            ProductionStage.Pack => 80,
            ProductionStage.Ship => 100,
            _ => 0
        };
    }
}

