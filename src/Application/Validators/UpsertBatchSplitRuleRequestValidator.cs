using FluentValidation;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Validators;

public class UpsertBatchSplitRuleRequestValidator : AbstractValidator<UpsertBatchSplitRuleRequest>
{
    public UpsertBatchSplitRuleRequestValidator()
    {
        RuleFor(x => x.MinQty)
            .GreaterThanOrEqualTo(1)
            .WithMessage("MinQty musi być >= 1.");

        RuleFor(x => x.MaxQty)
            .GreaterThanOrEqualTo(x => x.MinQty)
            .When(x => x.MaxQty.HasValue)
            .WithMessage("MaxQty musi być >= MinQty.");

        RuleFor(x => x.Percent)
            .InclusiveBetween(0.01m, 100m)
            .WithMessage("Percent musi być w zakresie 0.01..100.");

        RuleFor(x => x.MinBatchSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("MinBatchSize musi być >= 1.");

        RuleFor(x => x.MaxBatchesPerProject)
            .GreaterThanOrEqualTo(1)
            .When(x => x.MaxBatchesPerProject.HasValue)
            .WithMessage("MaxBatchesPerProject musi być >= 1.");
    }
}
