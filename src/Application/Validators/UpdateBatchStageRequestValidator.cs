using DataAccess.Enums;
using FluentValidation;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Validators;

public class UpdateBatchStageRequestValidator : AbstractValidator<UpdateBatchStageRequest>
{
    public UpdateBatchStageRequestValidator()
    {
        RuleFor(x => x.NewStage)
            .IsInEnum()
            .WithMessage("Nieprawidłowy etap produkcji.");
    }
}
